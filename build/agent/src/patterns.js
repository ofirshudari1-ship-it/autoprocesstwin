// Repeated-action detection + automation recommendations.
//
// This is the actual point of the system per the product goal: not "here's
// where your time went" (that's just the activity log), but "here's what you
// do over and over, how much time it costs, and what kind of tool/automation
// could remove it." Aimed at a business owner reviewing an employee's month,
// not just the employee themselves.
//
// Deliberately simple v1: keyword/heuristic category matching, not ML/NLP.
// Said so out loud in every output - see CATEGORY_RULES below. Good enough to
// be useful, honest about not being smarter than it is.

import { eventsBetween } from './db.js';

// Window titles are mostly "<something specific> - <app/site name>" (browser
// tabs, CRM record screens, etc). The last segment is usually the stable part
// - the thing that repeats - while the first segment is the variable part
// (customer name, file name, email subject). Grouping on (app_name + last
// title segment) catches "same CRM screen, different customer" without any
// NLP: e.g. "ישראל ישראלי - כרטיס לקוח - Fireberry" and "דנה כהן - כרטיס לקוח
// - Fireberry" both collapse to the same key.
function titleKey(appName, windowTitle) {
  const title = (windowTitle || '').trim();
  if (!title) return appName || 'לא ידוע';
  const parts = title.split(/\s+-\s+/).filter(Boolean);
  const stablePart = parts.length > 1 ? parts[parts.length - 1] : title;
  return `${appName || 'לא ידוע'} :: ${stablePart}`.toLowerCase();
}

// Category keyword rules -> {label, suggestion}. Checked against the app name
// + full window title (lowercased). First match wins. Extend this list as
// real usage reveals more categories - it's just a lookup table, not a model.
const CATEGORY_RULES = [
  {
    label: 'CRM / תיעוד לקוחות',
    keywords: [
      'fireberry', 'salesforce', 'hubspot', 'zoho', 'pipedrive', 'monday.com', 'monday ', 'crm',
      'priority', 'netsuite', 'freshsales', 'close.com', 'copper', 'sugarcrm',
      'כרטיס לקוח', 'לקוח חדש', 'פרטי לקוח', 'עמדת מכירה',
    ],
    suggestion: 'תיעוד חוזר ב-CRM הוא מהמועמדים הכי טובים לאוטומציה: תבניות שדות מוכנות מראש, ייבוא בבאסה (bulk import) מקובץ אחד, או אינטגרציה ישירה בין המערכת שמזינה את הנתונים לבין ה-CRM (Zapier/Make/API) שמייתרת הזנה ידנית לגמרי.',
  },
  {
    label: 'חשבוניות / הנהלת חשבונות',
    keywords: [
      'חשבונית', 'חשבוניות', 'invoice', 'quickbooks', 'freshbooks', 'green invoice',
      'חשבונית ירוקה', 'icount', 'rivhit', 'חשבשבת', 'זוהו חשבוניות',
    ],
    suggestion: 'הזנת חשבוניות ידנית בדרך כלל אפשר להחליף בהעלאה אוטומטית עם OCR (סריקת הקובץ ומילוי השדות לבד), או חיבור ישיר בין מערכת ההנה"ח לתיקיית החשבוניות הנכנסות שמייתר הזנה שורה-שורה.',
  },
  {
    label: 'תמיכה / כרטיסי שירות',
    keywords: ['zendesk', 'freshdesk', 'intercom', 'jira', 'helpdesk', 'כרטיס תמיכה', 'טיקט', 'service desk'],
    suggestion: 'אם זו אותה תשובה/סיווג שחוזר על כרטיסי תמיכה - תבניות תשובה מוכנות (canned responses), חוקי סיווג אוטומטיים, או צ׳אטבוט לשלב הראשוני יכולים לחסוך את רוב הזמן החוזר הזה.',
  },
  {
    label: 'העברת/ניהול קבצים',
    keywords: ['explorer.exe', 'winscp', 'filezilla', 'total commander', 'google drive', 'onedrive', 'dropbox', 'sharepoint', 'wetransfer', 'box.com'],
    suggestion: 'העברת קבצים חוזרת בדרך כלל אפשר לפתור עם סנכרון תיקיות מתוזמן (robocopy /MIR ב-Task Scheduler, rclone), או קיצור דרך שממפה את היעד הקבוע - בלי לנווט ידנית בכל פעם.',
  },
  {
    label: 'גיליונות / הזנת נתונים',
    keywords: ['excel', 'google sheets', 'sheets.google', 'numbers', 'airtable'],
    suggestion: 'הזנת נתונים חוזרת בגיליון היא בדרך כלל מועמדת מצוינת ל-macro/Apps Script קצר, או לחיבור ישיר בין המקור לגיליון (API/webhook) שמייתר העתק-הדבק ידני.',
  },
  {
    label: 'מייל',
    keywords: ['outlook', 'gmail', 'mail.google', 'mail.yahoo', 'thunderbird'],
    suggestion: 'אם זו אותה תבנית תשובה שחוזרת - תבניות מייל שמורות (Quick Parts באאוטלוק, Templates בג׳ימייל) או כלל אוטומטי (Rule/Filter) יכולים לחסוך הרבה מהזמן הזה.',
  },
  {
    label: 'הודעות / צ׳אט עסקי',
    keywords: ['whatsapp', 'web.whatsapp', 'slack', 'telegram', 'טלגרם'],
    suggestion: 'אם אלו אותן תשובות שחוזרות בצ׳אט - הודעות מהירות שמורות, אוטומציית תשובה ראשונית, או בוט לשאלות נפוצות יכולים לצמצם משמעותית את זמן ההקלדה החוזרת.',
  },
  {
    label: 'תמלול/תיעוד פגישות',
    keywords: ['zoom', 'teams', 'meet.google', 'meetscribe', 'webex', 'skype', 'gotomeeting'],
    suggestion: 'אם חלק מהזמן הוא תיעוד/סיכום ידני של פגישות - כלי תמלול+סיכום אוטומטי (למשל MeetScribe שכבר יש לך בתיקייה הזו) יכול לחסוך את שלב הכתיבה הידנית.',
  },
];

function categorize(appName, windowTitle) {
  const haystack = `${appName || ''} ${windowTitle || ''}`.toLowerCase();
  for (const rule of CATEGORY_RULES) {
    if (rule.keywords.some((kw) => haystack.includes(kw))) {
      return rule;
    }
  }
  return null;
}

const GENERIC_SUGGESTION =
  'לא זוהתה קטגוריה ספציפית - אבל התדירות/משך הזמן כאן מספיקים כדי שכדאי לבדוק ידנית אם יש דרך לייעל: תבנית מוכנה, קיצור מקלדת, או כלי ייעודי לאפליקציה הזו.';

// threshold for flagging something as an actual "automation candidate" rather
// than just background noise - needs real repetition, not a fluke.
const MIN_MINUTES_TO_FLAG = 20;
const MIN_OCCURRENCES_TO_FLAG = 4;

function groupByPattern(events, minutesById) {
  const groups = new Map();
  for (const ev of events) {
    const key = titleKey(ev.app_name, ev.window_title);
    if (!groups.has(key)) {
      groups.set(key, {
        key,
        appName: ev.app_name || 'לא ידוע',
        titleSample: ev.window_title || '',
        occurrences: 0,
        minutes: 0,
        days: new Set(),
      });
    }
    const g = groups.get(key);
    g.occurrences += 1;
    g.minutes += minutesById.get(ev.id) || 0;
    g.days.add(new Date(ev.ts).toDateString());
  }
  return groups;
}

function median(numbers) {
  const sorted = [...numbers].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
}

// Real per-event duration instead of one flat average across the whole
// window. The old approach (total span / event count) gets badly distorted
// by any real gap in recording - a weekend, the computer being off, the
// recorder being paused for a day - which inflates the "minutes per event"
// estimate for EVERY event in the window, not just the ones near the gap.
// Here, each event's duration is the real gap to the next event, individually
// capped so an actual idle/paused stretch doesn't inflate one bucket. The
// very last event has no "next" to measure against, so it falls back to the
// median of the real gaps (capped), not a fixed guess.
const MAX_EVENT_MINUTES = 5;

function computeEventMinutes(sortedEvents) {
  const capMs = MAX_EVENT_MINUTES * 60000;
  const minutesById = new Map();
  const gapsMinutes = [];
  for (let i = 0; i < sortedEvents.length - 1; i++) {
    const gapMs = sortedEvents[i + 1].ts - sortedEvents[i].ts;
    const minutes = Math.min(Math.max(gapMs, 0), capMs) / 60000;
    minutesById.set(sortedEvents[i].id, minutes);
    gapsMinutes.push(minutes);
  }
  if (sortedEvents.length > 0) {
    const lastEvent = sortedEvents[sortedEvents.length - 1];
    minutesById.set(lastEvent.id, gapsMinutes.length ? median(gapsMinutes) : MAX_EVENT_MINUTES);
  }
  return minutesById;
}

// Confidence score (product-improvement pass, 2026-09-16): how *consistently*
// a pattern repeats, not just how much total time it ate. Two patterns can
// have identical total minutes - one spread evenly across 20 different days
// (a real daily habit, high confidence it'll keep happening and is worth
// automating), the other from a single 3-hour burst on one day (could be a
// one-off project, not a repeating chore). Deliberately simple and explained
// in every output, same honesty bar as the category rules above - this is
// NOT a ML confidence score, just two ratios blended:
//   - day coverage: distinctDays / periodDays (capped at 1) - "how often does
//     this show up across the whole window", the stronger signal
//   - occurrence density: occurrences / (distinctDays * 3) (capped at 1) -
//     "when it does show up, does it repeat within the day too", a weaker
//     secondary signal so a pattern seen once/day for a month still scores
//     reasonably without needing multiple hits per day
const CONFIDENCE_DAY_WEIGHT = 0.7;
const CONFIDENCE_DENSITY_WEIGHT = 0.3;

function computeConfidence(occurrences, distinctDays, periodDays) {
  const dayCoverage = Math.min(1, distinctDays / Math.max(1, periodDays));
  const density = Math.min(1, occurrences / Math.max(1, distinctDays * 3));
  const score = Math.round(100 * (CONFIDENCE_DAY_WEIGHT * dayCoverage + CONFIDENCE_DENSITY_WEIGHT * density));
  const label = score >= 70 ? 'high' : score >= 40 ? 'medium' : 'low';
  return { score, label };
}

// Trend needs a real comparison, not a guess: split the requested window in
// half and compare this-half vs previous-half minutes for the same pattern
// key. Purely derived from what was actually recorded - no assumed savings
// percentage anywhere, that would be a number I can't actually back up.
const TREND_STABLE_BAND = 0.15; // within +-15% counts as "stable", not noise-driven up/down

function classifyTrend(recentMinutes, previousMinutes) {
  if (previousMinutes < 1 && recentMinutes >= 1) return { direction: 'new', percent: null };
  if (previousMinutes < 1 && recentMinutes < 1) return { direction: 'stable', percent: 0 };
  const change = (recentMinutes - previousMinutes) / previousMinutes;
  if (Math.abs(change) <= TREND_STABLE_BAND) return { direction: 'stable', percent: Math.round(change * 100) };
  return { direction: change > 0 ? 'up' : 'down', percent: Math.round(change * 100) };
}

export function analyzePatterns(days = 30, ignoredKeys = []) {
  const ignoredSet = new Set(ignoredKeys);
  const end = Date.now();
  const start = end - days * 24 * 60 * 60 * 1000;
  const midpoint = start + (end - start) / 2;

  const allEvents = eventsBetween(start, end).filter((e) => !e.filtered);

  if (allEvents.length === 0) {
    return { periodDays: days, totalMinutesTracked: 0, patterns: [], categoryBreakdown: [] };
  }

  // Computed once over the full sorted window so a later split into
  // recent/previous halves (for trend) looks up the same real per-event
  // duration rather than recomputing it - a subset's own first/last event
  // isn't a meaningful boundary for what its "next event" gap should be.
  const minutesById = computeEventMinutes(allEvents);

  const fullGroups = groupByPattern(allEvents, minutesById);

  // trend comparison only makes sense with enough history on both sides -
  // below ~6 days total, a "half" is too thin to mean anything, so trend is
  // omitted entirely rather than shown misleadingly.
  const canComputeTrend = days >= 6;
  let recentGroups = null;
  let previousGroups = null;
  if (canComputeTrend) {
    const recentEvents = allEvents.filter((e) => e.ts >= midpoint);
    const previousEvents = allEvents.filter((e) => e.ts < midpoint);
    recentGroups = groupByPattern(recentEvents, minutesById);
    previousGroups = groupByPattern(previousEvents, minutesById);
  }

  const patterns = [...fullGroups.values()]
    .filter((g) => !ignoredSet.has(g.key))
    .map((g) => {
      const category = categorize(g.appName, g.titleSample);
      const isCandidate = g.minutes >= MIN_MINUTES_TO_FLAG && g.occurrences >= MIN_OCCURRENCES_TO_FLAG;
      const confidence = computeConfidence(g.occurrences, g.days.size, days);

      let trend = null;
      if (canComputeTrend) {
        const recentMinutes = recentGroups.has(g.key) ? recentGroups.get(g.key).minutes : 0;
        const previousMinutes = previousGroups.has(g.key) ? previousGroups.get(g.key).minutes : 0;
        trend = classifyTrend(recentMinutes, previousMinutes);
      }

      return {
        key: g.key,
        appName: g.appName,
        titleSample: g.titleSample,
        minutes: Math.round(g.minutes),
        occurrences: g.occurrences,
        distinctDays: g.days.size,
        categoryLabel: category ? category.label : null,
        recommendation: isCandidate ? (category ? category.suggestion : GENERIC_SUGGESTION) : null,
        isAutomationCandidate: isCandidate,
        trendDirection: trend ? trend.direction : null,
        trendPercent: trend ? trend.percent : null,
        confidenceScore: isCandidate ? confidence.score : null,
        confidenceLabel: isCandidate ? confidence.label : null,
      };
    })
    .sort((a, b) => b.minutes - a.minutes);

  const totalMinutesTracked = Math.round(allEvents.reduce((sum, ev) => sum + (minutesById.get(ev.id) || 0), 0));

  // Category-level breakdown (product-improvement pass, 2026-09-16): the
  // per-pattern list answers "what repeats", this answers "where does the
  // recorded time go overall" - the category/app-level time breakdown that
  // RescueTime/Timely-style tools lead with. Aggregated across ALL patterns
  // (not just automation candidates) so a category's total isn't understated
  // just because no single pattern within it crossed the candidate threshold.
  // "Not categorized" bucket is kept last and separate rather than hidden, so
  // the total always reconciles with totalMinutesTracked.
  const categoryTotals = new Map();
  for (const g of fullGroups.values()) {
    if (ignoredSet.has(g.key)) continue;
    const category = categorize(g.appName, g.titleSample);
    const label = category ? category.label : 'לא מסווג';
    if (!categoryTotals.has(label)) categoryTotals.set(label, { categoryLabel: label, minutes: 0, patternCount: 0 });
    const entry = categoryTotals.get(label);
    entry.minutes += g.minutes;
    entry.patternCount += 1;
  }
  const categoryBreakdown = [...categoryTotals.values()]
    .map((c) => ({
      categoryLabel: c.categoryLabel,
      minutes: Math.round(c.minutes),
      patternCount: c.patternCount,
      percent: totalMinutesTracked > 0 ? Math.round((c.minutes / totalMinutesTracked) * 100) : 0,
    }))
    .sort((a, b) => b.minutes - a.minutes);

  // דפוסים שהמשתמש סימן "כבר טיפלתי בזה" - מוצג ברשימה נפרדת בממשק כדי
  // שאפשר יהיה לבטל בטעות ("לא ידעתי שזה נעלם לצמיתות"), לא רק להעלים לצמיתות
  // בלי אפשרות חזרה. רק דפוסים שעדיין קיימים בנתונים האחרונים - מפתח מתועלם
  // שאין לו יותר פעילות תואמת אין טעם להציג.
  const ignoredPatterns = [...fullGroups.values()]
    .filter((g) => ignoredSet.has(g.key))
    .map((g) => ({
      key: g.key,
      appName: g.appName,
      titleSample: g.titleSample,
      categoryLabel: (categorize(g.appName, g.titleSample) || {}).label || null,
    }));

  return { periodDays: days, totalMinutesTracked, patterns, ignoredPatterns, categoryBreakdown };
}
