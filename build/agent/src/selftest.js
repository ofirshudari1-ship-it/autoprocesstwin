// Deterministic checks for logic that the C# SelfTest can't easily exercise
// itself: the privacy filter (needs known inputs, not whatever window happens
// to be active) and retention purge (needs a fake old row, not real data).
// Run standalone: `node selftest.js`. Exits 0 on pass, 1 on fail.
import { createPrivacyFilter } from './privacyFilter.js';
import { insertEvent, purgeOlderThan, eventsBetween, openDb, closeDb } from './db.js';
import { analyzePatterns } from './patterns.js';
import { loadIgnoredPatterns, addIgnoredPattern, removeIgnoredPattern } from './ignoredPatterns.js';
import { mkdirSync, writeFileSync, existsSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import { PATHS } from './config.js';

let failed = false;
function check(name, condition) {
  if (condition) {
    console.log('PASS: ' + name);
  } else {
    console.log('FAIL: ' + name);
    failed = true;
  }
}

// --- Privacy filter: known inputs, no dependency on the real active window ---
const filter = createPrivacyFilter({
  excluded_apps: ['1password', 'bank'],
  excluded_title_keywords: ['סיסמה', 'password'],
});

check('excludes app by name (case-insensitive)', filter.isExcluded('1Password', 'some title') === true);
check('excludes by title keyword', filter.isExcluded('Chrome', 'הזן סיסמה כאן') === true);
check('excludes by title keyword (english)', filter.isExcluded('Chrome', 'Enter your password') === true);
check('does NOT exclude an unrelated app/title', filter.isExcluded('Google Chrome', 'GitHub - some repo') === false);

const appReason = filter.checkExclusion('1Password', 'some title');
check('checkExclusion reports which app matched', appReason.reason === 'app:1password');
const kwReason = filter.checkExclusion('Chrome', 'הזן סיסמה כאן');
check('checkExclusion reports which keyword matched', kwReason.reason === 'keyword:סיסמה');
const noReason = filter.checkExclusion('Chrome', 'GitHub');
check('checkExclusion reports null reason when not excluded', noReason.excluded === false && noReason.reason === null);
check('redacts a 16-digit card-like number', filter.redact('card: 4111 1111 1111 1111').includes('[REDACTED-CARD]'));
check('redacts a 9-digit ID-like number', filter.redact('ת"ז 123456789').includes('[REDACTED-ID]'));
check('leaves normal text alone', filter.redact('hello world') === 'hello world');

// --- Retention purge: insert a fake old row + a fake recent row, purge, verify ---
mkdirSync(PATHS.SCREENSHOTS_DIR, { recursive: true });
openDb();

const oldTs = Date.now() - 40 * 24 * 60 * 60 * 1000; // 40 days ago
const recentTs = Date.now() - 1000;
const oldScreenshot = join(PATHS.SCREENSHOTS_DIR, 'selftest-old.png');
writeFileSync(oldScreenshot, Buffer.from([0])); // dummy file, just needs to exist

insertEvent({ ts: oldTs, appName: 'SelfTestOldApp', windowTitle: 'old', screenshotPath: oldScreenshot, ocrText: null, filtered: false });
insertEvent({ ts: recentTs, appName: 'SelfTestRecentApp', windowTitle: 'recent', screenshotPath: null, ocrText: null, filtered: false });
insertEvent({ ts: recentTs + 1, appName: 'SelfTestFilteredApp', windowTitle: null, screenshotPath: null, ocrText: null, filtered: true, filterReason: 'app:selftestfilteredapp' });

const filteredRow = eventsBetween(recentTs, recentTs + 2000).find(e => e.app_name === 'SelfTestFilteredApp');
check('filter_reason persists through insert+read', !!filteredRow && filteredRow.filter_reason === 'app:selftestfilteredapp');

const beforePurge = eventsBetween(oldTs - 1000, recentTs + 1000);
check('both test rows inserted', beforePurge.some(e => e.app_name === 'SelfTestOldApp') && beforePurge.some(e => e.app_name === 'SelfTestRecentApp'));

const purgedCount = purgeOlderThan(30); // retention_days=30, oldTs is 40 days ago
check('purge reports removing >= 1 row', purgedCount >= 1);

const afterPurge = eventsBetween(oldTs - 1000, recentTs + 1000);
check('old row is gone after purge', !afterPurge.some(e => e.app_name === 'SelfTestOldApp'));
check('recent row survived purge', afterPurge.some(e => e.app_name === 'SelfTestRecentApp'));
check('old screenshot file was deleted', !existsSync(oldScreenshot));

// cleanup: remove the recent test row too, so this doesn't pollute real stats
try {
  const db = openDb();
  db.prepare("DELETE FROM activity_events WHERE app_name IN ('SelfTestOldApp', 'SelfTestRecentApp', 'SelfTestFilteredApp')").run();
} catch (e) {
  console.log('WARN: cleanup of test rows failed: ' + e.message);
}
if (existsSync(oldScreenshot)) unlinkSync(oldScreenshot);

// --- Pattern detection: the actual point of the system. Insert synthetic
// repeated CRM visits under 5 different "customer names" (same static title
// suffix, different variable prefix - exactly the real-world shape) plus one
// genuine one-off, verify they collapse/don't-collapse correctly and the
// right recommendation fires. ---
const now = Date.now();
const day = 24 * 60 * 60 * 1000;

// --- Accuracy: a real gap in recording (weekend, recorder paused, computer
// off) must not inflate an unrelated pattern's minutes. The old approach
// (total window span / event count, one flat average applied to every event)
// got badly distorted by exactly this - a single multi-day gap anywhere in
// the window would balloon the "minutes per event" estimate for every event,
// including a tight, genuinely-short cluster elsewhere. Self-contained:
// inserts and cleans up its own rows so it doesn't affect the tests below. ---
for (let i = 0; i < 10; i++) {
  insertEvent({ ts: now - i * 30000, appName: 'SelfTestGapAccuracy', windowTitle: 'x - SelfTestGapAccuracy', screenshotPath: null, ocrText: null, filtered: false });
}
insertEvent({ ts: now - 10 * day, appName: 'SelfTestGapAnchor', windowTitle: 'x - SelfTestGapAnchor', screenshotPath: null, ocrText: null, filtered: false });
const gapAnalysis = analyzePatterns(30);
const gapPattern = gapAnalysis.patterns.find((p) => p.appName === 'SelfTestGapAccuracy');
check(
  'a 10-day gap elsewhere in the window does not inflate a tight 30s-apart cluster\'s minutes',
  !!gapPattern && gapPattern.minutes <= 10 // 10 events ~30s apart is ~5 real minutes; the old flat-average bug would report closer to a full day's worth
);
try {
  const db = openDb();
  db.prepare("DELETE FROM activity_events WHERE app_name IN ('SelfTestGapAccuracy', 'SelfTestGapAnchor')").run();
} catch (e) {
  console.log('WARN: cleanup of gap-accuracy test rows failed: ' + e.message);
}

// --- Pattern detection: the actual point of the system. Insert synthetic
// repeated CRM visits under 5 different "customer names" (same static title
// suffix, different variable prefix - exactly the real-world shape) plus one
// genuine one-off, verify they collapse/don't-collapse correctly and the
// right recommendation fires. ---
// Pre-cleanup: wipe any rows left by a previous run so the distinct-days and
// occurrences counts are deterministic regardless of how many times selftest
// has been run before. This is the right fix - cleanup at end alone isn't
// sufficient if a previous run crashed between insert and cleanup.
try {
  const db = openDb();
  db.prepare("DELETE FROM activity_events WHERE app_name IN ('SelfTestBrowser', 'SelfTestNotepad', 'SelfTestTrendApp', 'SelfTestDismissApp', 'SelfTestInvoiceApp', 'SelfTestSupportApp')").run();
} catch (e) { /* ignore */ }

const customers = ['SelfTestCustomerA', 'SelfTestCustomerB', 'SelfTestCustomerC', 'SelfTestCustomerD', 'SelfTestCustomerE'];
for (let d = 0; d < 5; d++) {
  for (let i = 0; i < 3; i++) {
    insertEvent({ ts: now - d * day - i * 60000, appName: 'SelfTestBrowser', windowTitle: customers[d] + ' - Fireberry', screenshotPath: null, ocrText: null, filtered: false });
  }
}
insertEvent({ ts: now - 5000, appName: 'SelfTestNotepad', windowTitle: 'SelfTestOneOffNote', screenshotPath: null, ocrText: null, filtered: false });

const analysis = analyzePatterns(30);
const crmPattern = analysis.patterns.find((p) => p.appName === 'SelfTestBrowser');
const oneOff = analysis.patterns.find((p) => p.appName === 'SelfTestNotepad');

check('pattern grouping collapses 5 different customer names into one pattern', !!crmPattern && crmPattern.occurrences === 15);
check('pattern grouping counts distinct days correctly', !!crmPattern && crmPattern.distinctDays === 5);
check('repeated CRM pattern is flagged as an automation candidate', !!crmPattern && crmPattern.isAutomationCandidate === true);
check('repeated CRM pattern gets the CRM category + a non-empty recommendation', !!crmPattern && crmPattern.categoryLabel === 'CRM / תיעוד לקוחות' && !!crmPattern.recommendation);
check('genuine one-off activity is NOT flagged as a candidate', !!oneOff && oneOff.isAutomationCandidate === false);

// --- Confidence score: report-only signal, computed only for automation
// candidates (not shown for everything, so it doesn't imply false precision
// on noise). The 15-event/5-day CRM pattern above spans 5 of the requested
// 30 days with 3 hits/day - should land solidly in "high" or "medium", never
// null, and the score itself should be a real 0-100 number, not a stub. ---
check('automation-candidate pattern gets a numeric confidence score in [0,100]', !!crmPattern && typeof crmPattern.confidenceScore === 'number' && crmPattern.confidenceScore >= 0 && crmPattern.confidenceScore <= 100);
check('automation-candidate pattern gets a confidence label', !!crmPattern && ['high', 'medium', 'low'].includes(crmPattern.confidenceLabel));
check('non-candidate one-off pattern has no confidence score (not enough repetition to score)', !!oneOff && oneOff.confidenceScore === null && oneOff.confidenceLabel === null);

// --- Category breakdown: report-only rollup across ALL patterns (not just
// candidates), the "where does the time go overall" view a manager wants
// monthly per the product goal in SPEC.md. Built from the same 30-day
// analysis above, which already has the CRM/invoice/support test rows. ---
const crmBreakdown = analysis.categoryBreakdown.find((c) => c.categoryLabel === 'CRM / תיעוד לקוחות');
check('category breakdown includes the CRM category with matching pattern count', !!crmBreakdown && crmBreakdown.patternCount >= 1);
check('category breakdown percentages are real numbers derived from total tracked time', !!crmBreakdown && typeof crmBreakdown.percent === 'number' && crmBreakdown.percent >= 0 && crmBreakdown.percent <= 100);
check('category breakdown is sorted by minutes descending', analysis.categoryBreakdown.every((c, i, arr) => i === 0 || arr[i - 1].minutes >= c.minutes));

// --- Categories added this round (invoicing, support tickets) - just enough
// events to get a pattern back; categorization doesn't require passing the
// automation-candidate threshold. ---
insertEvent({ ts: now - 2000, appName: 'SelfTestInvoiceApp', windowTitle: 'לקוח X - חשבונית - SelfTestInvoiceApp', screenshotPath: null, ocrText: null, filtered: false });
insertEvent({ ts: now - 1000, appName: 'SelfTestSupportApp', windowTitle: 'טיקט 123 - SelfTestSupportApp', screenshotPath: null, ocrText: null, filtered: false });
const categoryCheck = analyzePatterns(30);
const invoicePattern = categoryCheck.patterns.find((p) => p.appName === 'SelfTestInvoiceApp');
const supportPattern = categoryCheck.patterns.find((p) => p.appName === 'SelfTestSupportApp');
check('invoicing keyword is categorized correctly', !!invoicePattern && invoicePattern.categoryLabel === 'חשבוניות / הנהלת חשבונות');
check('support-ticket keyword is categorized correctly', !!supportPattern && supportPattern.categoryLabel === 'תמיכה / כרטיסי שירות');

// --- Trend: light activity in the older half of the window, heavy in the
// recent half -> should classify as "up" with a real, computed percentage
// (not a guess). Uses a 30-day window so both halves have real headroom. ---
for (let d = 25; d >= 20; d--) {
  insertEvent({ ts: now - d * day, appName: 'SelfTestTrendApp', windowTitle: 'x - SelfTestTrendApp', screenshotPath: null, ocrText: null, filtered: false });
}
for (let d = 5; d >= 0; d--) {
  for (let i = 0; i < 5; i++) {
    insertEvent({ ts: now - d * day - i * 30000, appName: 'SelfTestTrendApp', windowTitle: 'x - SelfTestTrendApp', screenshotPath: null, ocrText: null, filtered: false });
  }
}
const trendAnalysis = analyzePatterns(30);
const trendPattern = trendAnalysis.patterns.find((p) => p.appName === 'SelfTestTrendApp');
check('trend detects real growth between the two halves of the window as "up"', !!trendPattern && trendPattern.trendDirection === 'up');
check('trend percent is a real computed number, not null/zero', !!trendPattern && typeof trendPattern.trendPercent === 'number' && trendPattern.trendPercent > 0);

// --- Dismiss/ignore: a pattern the user marked "already handled" should
// disappear from future analysis, and un-ignoring should bring it back. ---
insertEvent({ ts: now - 1000, appName: 'SelfTestDismissApp', windowTitle: 'x - SelfTestDismissApp', screenshotPath: null, ocrText: null, filtered: false });
const beforeIgnore = analyzePatterns(30, loadIgnoredPatterns());
const dismissTarget = beforeIgnore.patterns.find((p) => p.appName === 'SelfTestDismissApp');
check('dismiss target pattern exists before being ignored', !!dismissTarget);

if (dismissTarget) {
  addIgnoredPattern(dismissTarget.key);
  const afterIgnore = analyzePatterns(30, loadIgnoredPatterns());
  check('ignored pattern is excluded from analysis', !afterIgnore.patterns.some((p) => p.appName === 'SelfTestDismissApp'));
  check('ignored pattern appears in ignoredPatterns with metadata (so the GUI can offer an undo)', afterIgnore.ignoredPatterns.some((p) => p.appName === 'SelfTestDismissApp' && p.key === dismissTarget.key));

  removeIgnoredPattern(dismissTarget.key);
  const afterUnignore = analyzePatterns(30, loadIgnoredPatterns());
  check('un-ignoring brings the pattern back', afterUnignore.patterns.some((p) => p.appName === 'SelfTestDismissApp'));
  check('un-ignoring removes it from ignoredPatterns too', !afterUnignore.ignoredPatterns.some((p) => p.appName === 'SelfTestDismissApp'));
}

try {
  const db = openDb();
  db.prepare("DELETE FROM activity_events WHERE app_name IN ('SelfTestBrowser', 'SelfTestNotepad', 'SelfTestTrendApp', 'SelfTestDismissApp', 'SelfTestInvoiceApp', 'SelfTestSupportApp')").run();
} catch (e) {
  console.log('WARN: cleanup of pattern test rows failed: ' + e.message);
}

closeDb();

// --- AI insights: opt-in, off by default - must degrade gracefully (no
// crash, no network call, no hang) when disabled or missing a key, since
// that's the state this repo ships in. Spawned as a real subprocess (not
// imported) because ai-insights.js runs main() as a load-time side effect. ---
try {
  // PATHS.ROOT is the writable user-data root (%LOCALAPPDATA%\AutoProcessTwin)
  // since 0.5.1 - the ai-insights.js script itself still ships read-only next
  // to the exe, under PATHS.INSTALL_ROOT.
  const out = execFileSync(process.execPath, [join(PATHS.INSTALL_ROOT, 'agent', 'src', 'ai-insights.js')], { encoding: 'utf-8', timeout: 15000 });
  const parsed = JSON.parse(out.trim());
  check('ai-insights.js prints valid JSON with an enabled boolean', typeof parsed.enabled === 'boolean');
  if (parsed.enabled === false) {
    check('ai-insights.js reports a known reason when disabled', ['disabled', 'no-key', 'no-config'].includes(parsed.reason));
  }
} catch (e) {
  check('ai-insights.js runs without crashing (currently AI-off in this repo)', false);
  console.log('  (error: ' + e.message + ')');
}

console.log(failed ? '=== SELFTEST FAIL ===' : '=== SELFTEST ALL PASS ===');
process.exit(failed ? 1 : 0);
