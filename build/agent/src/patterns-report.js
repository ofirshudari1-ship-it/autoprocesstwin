// CLI entry point for the GUI's "המלצות אוטומציה" tab: runs analyzePatterns()
// and prints JSON to stdout. Also writes a human-readable markdown copy to
// reports/ so it's inspectable outside the app too.
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { analyzePatterns } from './patterns.js';
import { PATHS } from './config.js';
import { closeDb } from './db.js';
import { loadIgnoredPatterns, addIgnoredPattern, removeIgnoredPattern } from './ignoredPatterns.js';

function formatHours(minutes) {
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  if (h === 0) return `${m} דק'`;
  return `${h} שע' ${m} דק'`;
}

function formatTrend(direction, percent) {
  if (!direction) return null;
  if (direction === 'new') return 'חדש בתקופה הזו';
  if (direction === 'stable') return 'יציב';
  const sign = percent > 0 ? '+' : '';
  const arrow = direction === 'up' ? '↑' : '↓';
  return `${arrow} ${sign}${percent}% לעומת המחצית הקודמת של התקופה`;
}

// "כמה אפשר לסמוך שזה יחזור" - לא ציון AI, שילוב של שני יחסים פשוטים
// (כיסוי ימים + צפיפות חזרות). ר' patterns.js: computeConfidence להסבר מלא.
function formatConfidence(label) {
  if (label === 'high') return 'ביטחון גבוה';
  if (label === 'medium') return 'ביטחון בינוני';
  if (label === 'low') return 'ביטחון נמוך';
  return null;
}

function toMarkdown(result) {
  const lines = [];
  lines.push(`# פעולות חוזרות והמלצות אוטומציה — ${result.periodDays} הימים האחרונים`);
  lines.push('');
  lines.push(`נצפו ${formatHours(result.totalMinutesTracked)} של פעילות מוקלטת בסך הכול בתקופה הזו.`);
  lines.push('');

  if (result.categoryBreakdown && result.categoryBreakdown.length > 0) {
    lines.push('## פילוח לפי קטגוריה');
    lines.push('');
    lines.push('| קטגוריה | זמן | % מהזמן המוקלט | דפוסים |');
    lines.push('|---|---|---|---|');
    for (const c of result.categoryBreakdown) {
      lines.push(`| ${c.categoryLabel} | ${formatHours(c.minutes)} | ${c.percent}% | ${c.patternCount} |`);
    }
    lines.push('');
  }

  const candidates = result.patterns.filter((p) => p.isAutomationCandidate);
  if (candidates.length === 0) {
    lines.push('_עדיין אין מספיק חזרות מובהקות כדי להמליץ על משהו - צריך יותר ימים של הקלטה, או שפשוט אין הרבה פעולות חוזרות (טוב!)._');
  } else {
    lines.push('## מועמדים לאוטומציה');
    lines.push('');
    for (const p of candidates) {
      lines.push(`### ${p.categoryLabel || p.appName} — ${formatHours(p.minutes)}, ${p.occurrences} פעמים, ${p.distinctDays} ימים שונים`);
      lines.push(`דוגמה: \`${p.titleSample}\``);
      const trendText = formatTrend(p.trendDirection, p.trendPercent);
      if (trendText) lines.push(`מגמה: ${trendText}`);
      const confidenceText = formatConfidence(p.confidenceLabel);
      if (confidenceText) lines.push(`${confidenceText} (${p.confidenceScore}/100) - עד כמה עקבי הדפוס הזה על פני התקופה`);
      lines.push('');
      lines.push(p.recommendation);
      lines.push('');
    }
  }

  const rest = result.patterns.filter((p) => !p.isAutomationCandidate).slice(0, 10);
  if (rest.length > 0) {
    lines.push('## שאר הפעילות (מתחת לסף לזיהוי אוטומטי)');
    lines.push('');
    for (const p of rest) {
      lines.push(`- ${p.appName} — ${formatHours(p.minutes)} (${p.occurrences} פעמים)`);
    }
  }

  return lines.join('\n') + '\n';
}

// CSV export (product-improvement pass, 2026-09-16) - the format a manager
// actually drops into Excel/Sheets to sort/filter/pivot, alongside the
// read-it-once HTML/markdown. Only the automation candidates (the actionable
// rows), one per line. UTF-8 BOM so Hebrew text opens correctly in Excel
// (without it, Excel misreads UTF-8 Hebrew as mojibake on Windows).
function csvEscape(value) {
  const s = String(value ?? '');
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

function toCsv(result) {
  const header = ['קטגוריה', 'אפליקציה', 'דוגמה', 'דקות', 'פעמים', 'ימים שונים', 'ביטחון (0-100)', 'מגמה', 'המלצה'];
  const rows = [header.join(',')];
  const candidates = result.patterns.filter((p) => p.isAutomationCandidate);
  for (const p of candidates) {
    rows.push([
      csvEscape(p.categoryLabel || ''),
      csvEscape(p.appName),
      csvEscape(p.titleSample),
      csvEscape(p.minutes),
      csvEscape(p.occurrences),
      csvEscape(p.distinctDays),
      csvEscape(p.confidenceScore ?? ''),
      csvEscape(formatTrend(p.trendDirection, p.trendPercent) || ''),
      csvEscape(p.recommendation),
    ].join(','));
  }
  return '﻿' + rows.join('\r\n') + '\r\n';
}

// Standalone styled HTML export - the thing you'd actually hand to a manager,
// not the markdown. Same color tokens as the app's own Theme.cs (dark slate +
// emerald) so it looks like it belongs to the same product, not a random
// export. Self-contained: no external assets, opens directly in any browser.
function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function trendBadgeHtml(direction, percent) {
  if (!direction) return '';
  if (direction === 'stable') return '<span class="trend stable">יציב</span>';
  if (direction === 'new') return '<span class="trend new">חדש</span>';
  const sign = percent > 0 ? '+' : '';
  const cls = direction === 'up' ? 'up' : 'down';
  const arrow = direction === 'up' ? '↑' : '↓';
  return `<span class="trend ${cls}">${arrow} ${sign}${percent}%</span>`;
}

function confidenceBadgeHtml(label, score) {
  if (!label) return '';
  const text = label === 'high' ? 'ביטחון גבוה' : label === 'medium' ? 'ביטחון בינוני' : 'ביטחון נמוך';
  return `<span class="confidence ${label}" title="עד כמה עקבי הדפוס לאורך התקופה - לא ציון AI, שילוב של כיסוי-ימים ותדירות">${text} · ${score}</span>`;
}

function toHtml(result) {
  const dateLabel = new Date().toLocaleDateString('he-IL');
  const candidates = result.patterns.filter((p) => p.isAutomationCandidate);
  const totalCandidateMinutes = candidates.reduce((sum, p) => sum + p.minutes, 0);

  const categoryBreakdown = result.categoryBreakdown || [];
  const categoryRowsHtml = categoryBreakdown.map((c) => `
    <div class="cat-row">
      <div class="cat-label">${escapeHtml(c.categoryLabel)}</div>
      <div class="cat-bar-track"><div class="cat-bar-fill" style="width:${Math.max(2, c.percent)}%"></div></div>
      <div class="cat-value">${formatHours(c.minutes)} · ${c.percent}%</div>
    </div>`).join('\n');
  const categorySectionHtml = categoryBreakdown.length > 0 ? `
    <h2>פילוח לפי קטגוריה</h2>
    <div class="cat-list">${categoryRowsHtml}</div>` : '';

  const cardsHtml = candidates.map((p) => `
    <div class="card">
      <div class="card-head">
        <div>
          <div class="card-title">${escapeHtml(p.categoryLabel || p.appName)}</div>
          <div class="card-sub">${escapeHtml(p.appName)} · דוגמה: ${escapeHtml(p.titleSample)}</div>
        </div>
        <div class="card-stats">
          <span class="minutes">${formatHours(p.minutes)}</span>
          ${trendBadgeHtml(p.trendDirection, p.trendPercent)}
        </div>
      </div>
      <div class="bar-track"><div class="bar-fill" style="width:${Math.max(3, Math.round((p.minutes / (candidates[0].minutes || 1)) * 100))}%"></div></div>
      <div class="meta">${p.occurrences} פעמים · ${p.distinctDays} ימים שונים ${confidenceBadgeHtml(p.confidenceLabel, p.confidenceScore)}</div>
      <div class="recommendation">${escapeHtml(p.recommendation)}</div>
    </div>`).join('\n');

  const rest = result.patterns.filter((p) => !p.isAutomationCandidate).slice(0, 10);
  const restRowsHtml = rest.map((p) => `
    <tr>
      <td>${escapeHtml(p.categoryLabel || p.appName)}</td>
      <td>${escapeHtml(p.appName)}</td>
      <td class="mono">${formatHours(p.minutes)}</td>
      <td class="mono">${p.occurrences}</td>
      <td class="mono">${p.distinctDays}</td>
    </tr>`).join('\n');
  const restSectionHtml = rest.length > 0 ? `
    <h2 style="margin-top:32px">שאר הפעילות (מתחת לסף לזיהוי אוטומטי)</h2>
    <table class="rest-table">
      <thead><tr><th>קטגוריה</th><th>אפליקציה</th><th>זמן</th><th>פעמים</th><th>ימים</th></tr></thead>
      <tbody>${restRowsHtml}</tbody>
    </table>` : '';

  return `<!doctype html>
<html lang="he" dir="rtl">
<head>
<meta charset="utf-8">
<title>AutoProcess Twin — דוח פעולות חוזרות</title>
<meta name="viewport" content="width=device-width, initial-scale=1">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Heebo:wght@400;500;700;800&family=JetBrains+Mono:wght@400;700&display=swap" rel="stylesheet">
<style>
  :root {
    --bg:#0F172A; --panel:#1E293B; --panel2:#273349; --border:#334155;
    --text:#E2E8F0; --muted:#94A3B8; --accent:#10B981; --accent-light:#0F3D30;
    --warn:#FBBF24; --danger:#F87171;
  }
  * { box-sizing: border-box; }
  body { margin:0; background:var(--bg); color:var(--text); font-family:'Heebo',system-ui,sans-serif; line-height:1.6; }
  header { background:#0B1220; padding:32px 24px; }
  header .inner { max-width:820px; margin:0 auto; }
  h1 { margin:0; font-size:24px; font-weight:800; }
  header p { margin:8px 0 0; color:var(--muted); font-size:13.5px; }
  main { max-width:820px; margin:0 auto; padding:28px 24px 60px; }
  .summary { display:flex; gap:14px; margin-bottom:32px; flex-wrap:wrap; }
  .stat { background:var(--panel); border:1px solid var(--border); border-radius:12px; padding:16px 18px; flex:1; min-width:160px; }
  .stat .n { font-family:'JetBrains Mono',monospace; font-size:26px; font-weight:700; color:var(--accent); }
  .stat .l { font-size:12.5px; color:var(--muted); margin-top:4px; }
  h2 { font-size:17px; font-weight:800; margin:0 0 16px; }
  .card { background:var(--panel); border:1px solid var(--border); border-radius:14px; padding:18px 20px; margin-bottom:14px; }
  .card-head { display:flex; justify-content:space-between; gap:12px; align-items:flex-start; }
  .card-title { font-weight:700; font-size:15px; }
  .card-sub { font-size:12px; color:var(--muted); margin-top:3px; }
  .card-stats { display:flex; align-items:center; gap:8px; white-space:nowrap; }
  .minutes { font-family:'JetBrains Mono',monospace; color:var(--accent); font-weight:700; font-size:13px; }
  .trend { font-family:'JetBrains Mono',monospace; font-size:11px; padding:2px 7px; border-radius:999px; }
  .trend.up { background:#3F2E0B; color:var(--warn); }
  .trend.down { background:var(--accent-light); color:var(--accent); }
  .trend.stable, .trend.new { background:var(--panel2); color:var(--muted); }
  .bar-track { height:6px; background:var(--panel2); border-radius:3px; margin:12px 0 10px; overflow:hidden; }
  .bar-fill { height:100%; background:var(--accent); border-radius:3px; }
  .meta { font-size:12px; color:var(--muted); margin-bottom:10px; }
  .recommendation { font-size:13.5px; }
  .empty { color:var(--muted); font-size:14px; padding:20px 0; }
  .rest-table { width:100%; border-collapse:collapse; margin-top:12px; font-size:13px; }
  .rest-table th { text-align:right; color:var(--muted); font-weight:600; padding:6px 10px; border-bottom:1px solid var(--border); }
  .rest-table td { padding:7px 10px; border-bottom:1px solid var(--border); }
  .rest-table tr:last-child td { border-bottom:none; }
  .mono { font-family:'JetBrains Mono',monospace; color:var(--accent); font-size:12px; }
  .confidence { font-family:'JetBrains Mono',monospace; font-size:10.5px; padding:1px 6px; border-radius:999px; margin-inline-start:6px; }
  .confidence.high { background:var(--accent-light); color:var(--accent); }
  .confidence.medium { background:#3F2E0B; color:var(--warn); }
  .confidence.low { background:var(--panel2); color:var(--muted); }
  .cat-list { margin-bottom:32px; }
  .cat-row { display:flex; align-items:center; gap:12px; padding:8px 0; border-bottom:1px solid var(--border); }
  .cat-row:last-child { border-bottom:none; }
  .cat-label { width:180px; flex:0 0 180px; font-size:13px; }
  .cat-bar-track { flex:1; height:8px; background:var(--panel2); border-radius:4px; overflow:hidden; }
  .cat-bar-fill { height:100%; background:var(--accent); border-radius:4px; }
  .cat-value { width:130px; flex:0 0 130px; text-align:left; font-family:'JetBrains Mono',monospace; font-size:12px; color:var(--muted); }
  footer { max-width:820px; margin:0 auto; padding:0 24px 40px; color:var(--muted); font-size:11.5px; }
</style>
</head>
<body>
  <header>
    <div class="inner">
      <h1>AutoProcess Twin — דוח פעולות חוזרות והמלצות אוטומציה</h1>
      <p>${result.periodDays} הימים האחרונים · הופק ${escapeHtml(dateLabel)} · זיהוי לפי מילות מפתח ותדירות, לא AI</p>
    </div>
  </header>
  <main>
    <div class="summary">
      <div class="stat"><div class="n">${formatHours(result.totalMinutesTracked)}</div><div class="l">סך פעילות מוקלטת בתקופה</div></div>
      <div class="stat"><div class="n">${candidates.length}</div><div class="l">מועמדים לאוטומציה</div></div>
      <div class="stat"><div class="n">${formatHours(totalCandidateMinutes)}</div><div class="l">מתוכם בפעולות חוזרות מזוהות</div></div>
    </div>
    ${categorySectionHtml}
    <h2>מועמדים לאוטומציה</h2>
    ${candidates.length > 0 ? cardsHtml : '<div class="empty">אין עדיין מספיק חזרות מובהקות בתקופה הזו כדי להמליץ על משהו.</div>'}
    ${restSectionHtml}
  </main>
  <footer>AutoProcess Twin · Phase 1 · הכל רץ מקומית, שום דבר לא נשלח לענן</footer>
</body>
</html>`;
}

function main() {
  // node patterns-report.js --ignore "<pattern key>"  ->  dismiss one pattern
  // permanently (already automated it / not relevant), don't run a full
  // analysis this call - the GUI calls analyze again right after anyway.
  if (process.argv[2] === '--ignore') {
    const key = process.argv[3];
    if (!key) {
      process.stdout.write(JSON.stringify({ error: 'missing key' }));
      closeDb();
      return;
    }
    addIgnoredPattern(key);
    process.stdout.write(JSON.stringify({ ok: true }));
    closeDb();
    return;
  }

  // node patterns-report.js --unignore "<pattern key>"  ->  undo a dismiss.
  // Mirrors --ignore exactly, so a "כבר טיפלתי בזה" click isn't a one-way
  // door with no escape hatch.
  if (process.argv[2] === '--unignore') {
    const key = process.argv[3];
    if (!key) {
      process.stdout.write(JSON.stringify({ error: 'missing key' }));
      closeDb();
      return;
    }
    removeIgnoredPattern(key);
    process.stdout.write(JSON.stringify({ ok: true }));
    closeDb();
    return;
  }

  const days = Number(process.argv[2]) || 30;
  const ignored = loadIgnoredPatterns();
  const result = analyzePatterns(days, ignored);
  process.stdout.write(JSON.stringify(result));

  mkdirSync(PATHS.REPORTS_DIR, { recursive: true });
  const dateSuffix = new Date().toISOString().slice(0, 10);
  const mdPath = join(PATHS.REPORTS_DIR, `automation-recommendations-${dateSuffix}.md`);
  const htmlPath = join(PATHS.REPORTS_DIR, `automation-recommendations-${dateSuffix}.html`);
  const csvPath = join(PATHS.REPORTS_DIR, `automation-recommendations-${dateSuffix}.csv`);
  writeFileSync(mdPath, toMarkdown(result), 'utf-8');
  writeFileSync(htmlPath, toHtml(result), 'utf-8');
  writeFileSync(csvPath, toCsv(result));

  closeDb();
}

main();
