// Daily activity log — conclusions from today's recording, not a "morning
// briefing" (the product doesn't do anything overnight to report on — see
// patterns-report.js for the actual point of the system: repeated-action
// detection + automation recommendations, across many days, not one).

import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { eventsBetween, closeDb } from './db.js';
import { loadGuardrailsConfig, PATHS } from './config.js';

function startOfDay(date) {
  const d = new Date(date);
  d.setHours(0, 0, 0, 0);
  return d.getTime();
}

function formatMinutes(mins) {
  const h = Math.floor(mins / 60);
  const m = Math.round(mins % 60);
  if (h === 0) return `${m} דק'`;
  return `${h} שע' ${m} דק'`;
}

function describeFilterReason(reason) {
  if (reason.startsWith('app:')) return `אפליקציה מוחרגת: "${reason.slice(4)}"`;
  if (reason.startsWith('keyword:')) return `מילת מפתח בכותרת החלון: "${reason.slice(8)}"`;
  if (reason === 'no-active-window') return 'לא היה חלון פעיל לזהות (למשל מסך נעול)';
  return reason;
}

function buildReport(targetDate = new Date()) {
  const dayStart = startOfDay(targetDate);
  const dayEnd = dayStart + 24 * 60 * 60 * 1000;
  const events = eventsBetween(dayStart, dayEnd);
  const guardrails = loadGuardrailsConfig();

  const dateLabel = new Date(dayStart).toLocaleDateString('he-IL');

  if (events.length === 0) {
    return `# יומן פעילות — ${dateLabel}\n\nלא נמצאה פעילות מוקלטת ליום זה. ` +
      `ודא שה-recorder (\`npm start\` בתיקיית agent) רץ במהלך היום.\n`;
  }

  // Per-event duration using real gap to next event, capped at 5 min each.
  // Same approach as patterns.js — a global average (totalSpan / count) gets
  // distorted by any pause (lunch, meeting, idle screen) inflating every app's
  // time equally. Here each event owns only the gap to the next one.
  const MAX_EVENT_MS = 5 * 60 * 1000;
  const nonFiltered = events.filter((e) => !e.filtered);
  const gapsMsList = [];
  const minutesByEvId = new Map();
  for (let i = 0; i < nonFiltered.length - 1; i++) {
    const gapMs = Math.min(Math.max(nonFiltered[i + 1].ts - nonFiltered[i].ts, 0), MAX_EVENT_MS);
    minutesByEvId.set(nonFiltered[i].id, gapMs / 60000);
    gapsMsList.push(gapMs / 60000);
  }
  if (nonFiltered.length > 0) {
    const sortedGaps = [...gapsMsList].sort((a, b) => a - b);
    const mid = Math.floor(sortedGaps.length / 2);
    const medianGap = sortedGaps.length === 0 ? 1
      : sortedGaps.length % 2 === 0 ? (sortedGaps[mid - 1] + sortedGaps[mid]) / 2
      : sortedGaps[mid];
    minutesByEvId.set(nonFiltered[nonFiltered.length - 1].id, medianGap);
  }

  const byApp = new Map();
  const filterReasons = new Map();
  let filteredCount = 0;

  for (const ev of events) {
    if (ev.filtered) {
      filteredCount++;
      const reason = ev.filter_reason || 'לא ידוע';
      filterReasons.set(reason, (filterReasons.get(reason) || 0) + 1);
      continue;
    }
    const key = ev.app_name || 'לא ידוע';
    byApp.set(key, (byApp.get(key) || 0) + (minutesByEvId.get(ev.id) || 0));
  }

  const ranked = [...byApp.entries()]
    .sort((a, b) => b[1] - a[1]);

  const lines = [];
  lines.push(`# יומן פעילות — ${dateLabel}`);
  lines.push('');
  lines.push(`אלה המסקנות מיומן הפעילות של היום: ${events.length} תצפיות נרשמו. ` +
    `${filteredCount} מתוכן סוננו על-ידי ה-Privacy Filter (יישומים רגישים) ולא נשמר תוכן עבורן.`);
  lines.push('');

  if (filterReasons.size > 0) {
    lines.push('## מה בדיוק סינן ה-Privacy Filter היום');
    lines.push('');
    const rankedReasons = [...filterReasons.entries()].sort((a, b) => b[1] - a[1]);
    for (const [reason, count] of rankedReasons) {
      lines.push(`- ${describeFilterReason(reason)} — ${count} ${count === 1 ? 'פעם' : 'פעמים'}`);
    }
    lines.push('');
  }
  lines.push('## איפה עבר הזמן');
  lines.push('');
  for (const [app, minutes] of ranked.slice(0, 10)) {
    lines.push(`- **${app}** — כ-${formatMinutes(minutes)}`);
  }
  lines.push('');
  lines.push('_רוצה לדעת מה מתוך זה חוזר על עצמו מספיק כדי לשווה אוטומציה? זה בטאב ' +
    '"המלצות אוטומציה" באפליקציה, לא כאן - זה דורש כמה ימים של יומן כדי לזהות דפוס אמיתי, לא יום אחד._');
  lines.push('');
  lines.push('## ה-Guardrails הפעילים כרגע');
  lines.push('');
  lines.push(`- מצב הרצה: \`${guardrails.execution_mode_default}\``);
  lines.push(`- הנחה אוטומטית מקסימלית: ${guardrails.pricing_boundaries?.max_auto_discount_percent}%`);
  lines.push(`- סגירת עסקה אוטומטית עד: ${guardrails.pricing_boundaries?.max_auto_close_amount_ils} ${guardrails.pricing_boundaries?.currency}`);
  lines.push('');
  lines.push('_(guardrails אלה עדיין לא אוכפים שום דבר בפועל — הם קונפיג ממתין לשלב הבא)_');
  lines.push('');

  return lines.join('\n');
}

function main() {
  const report = buildReport();
  mkdirSync(PATHS.REPORTS_DIR, { recursive: true });
  const filename = `activity-log-${new Date().toISOString().slice(0, 10)}.md`;
  const outPath = join(PATHS.REPORTS_DIR, filename);
  writeFileSync(outPath, report, 'utf-8');
  console.log(`Report written to ${outPath}`);
  closeDb();
}

main();
