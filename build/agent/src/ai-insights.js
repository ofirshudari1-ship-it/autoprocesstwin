// Optional, opt-in AI executive summary. CLI: `node ai-insights.js [days]`.
// Always prints exactly one JSON object to stdout and always exits 0 - this
// is called from the GUI on a background refresh, so it must never throw,
// never hang, and never block the rest of the app if it's disabled, has no
// key, or the network call fails.
//
// Privacy: only aggregated pattern stats (category, app name, minutes,
// occurrences, distinct days, trend) are sent. titleSample (which can contain
// real customer/file names) is stripped before anything leaves the machine.
// Nothing is sent unless config/ai.json has "enabled": true and an apiKey.
import { analyzePatterns } from './patterns.js';
import { loadIgnoredPatterns } from './ignoredPatterns.js';
import { loadAiConfig } from './config.js';
import { closeDb } from './db.js';

function stripForExport(patterns) {
  return patterns
    .filter((p) => p.isAutomationCandidate)
    .slice(0, 12)
    .map((p) => ({
      category: p.categoryLabel || p.appName,
      appName: p.appName,
      minutes: p.minutes,
      occurrences: p.occurrences,
      distinctDays: p.distinctDays,
      trendDirection: p.trendDirection,
      trendPercent: p.trendPercent,
    }));
}

function buildPrompt(periodDays, items) {
  const lines = [
    `להלן פעולות חוזרות שזוהו אוטומטית אצל עובד יחיד במהלך ${periodDays} הימים האחרונים (רק סטטיסטיקה מצטברת - בלי שמות לקוחות או תוכן):`,
    JSON.stringify(items, null, 2),
    '',
    'כתוב סיכום מנהלים קצר בעברית (עד 4-5 משפטים): מה בולט, איפה הכי משתלם להשקיע באוטומציה, ומה אפשר להגיד למנהל הישיר. תהיה קונקרטי וממוקד, בלי הקדמות מיותרות.',
  ];
  return lines.join('\n');
}

async function main() {
  const days = Number(process.argv[2]) || 30;
  let config;
  try {
    config = loadAiConfig();
  } catch (e) {
    process.stdout.write(JSON.stringify({ enabled: false, reason: 'no-config' }));
    closeDb();
    return;
  }

  // APT_AI_KEY: injected by the GUI as a decrypted-in-memory env var so the
  // key never needs to be stored in plain text in config/ai.json (DPAPI-
  // encrypted there). Fall back to config.apiKey for legacy plain-text configs.
  const apiKey = process.env.APT_AI_KEY || config.apiKey;
  if (!config.enabled || !apiKey) {
    process.stdout.write(JSON.stringify({ enabled: false, reason: !config.enabled ? 'disabled' : 'no-key' }));
    closeDb();
    return;
  }

  const ignored = loadIgnoredPatterns();
  const analysis = analyzePatterns(days, ignored);
  const items = stripForExport(analysis.patterns);
  closeDb();

  if (items.length === 0) {
    process.stdout.write(JSON.stringify({ enabled: true, summary: null, reason: 'no-candidates' }));
    return;
  }

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 20000);
    const res = await fetch(config.endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${apiKey}`,
      },
      body: JSON.stringify({
        model: config.model || 'gpt-4o-mini',
        messages: [{ role: 'user', content: buildPrompt(analysis.periodDays, items) }],
        temperature: 0.4,
      }),
      signal: controller.signal,
    });
    clearTimeout(timeout);

    if (!res.ok) {
      // Only expose the HTTP status, not the raw error body (may echo request fragments)
      process.stdout.write(JSON.stringify({ enabled: true, summary: null, error: `HTTP ${res.status}` }));
      return;
    }

    const data = await res.json();
    const summary = data && data.choices && data.choices[0] && data.choices[0].message ? data.choices[0].message.content : null;
    process.stdout.write(JSON.stringify({ enabled: true, summary: summary || null }));
  } catch (e) {
    process.stdout.write(JSON.stringify({ enabled: true, summary: null, error: String(e && e.message || e) }));
  }
}

main();
