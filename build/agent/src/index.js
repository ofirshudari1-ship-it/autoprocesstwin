// AutoProcess Twin — Phase 1 recorder ("Live Assist" data collection only).
//
// What this does: every `capture_interval_ms`, reads the active window's app
// name + title. If it's not privacy-filtered, takes a screenshot and (if
// tesseract is installed) OCRs it. Everything is written to a local SQLite
// file — nothing leaves this machine, nothing is sent anywhere.
//
// What this deliberately does NOT do: read keystroke content, listen to audio,
// or take any autonomous action (send email, touch a CRM, etc). See README.

import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import activeWindow from 'active-win';
import { captureScreen } from './screenshot.js';
import { loadPrivacyConfig, PATHS } from './config.js';
import { createPrivacyFilter } from './privacyFilter.js';
import { insertEvent, purgeOlderThan, closeDb } from './db.js';
import { extractText } from './ocr.js';

const privacyConfig = loadPrivacyConfig();
const filter = createPrivacyFilter(privacyConfig);
const intervalMs = privacyConfig.capture_interval_ms ?? 60000;
const retentionDays = privacyConfig.retention_days ?? 30;
const CLEANUP_INTERVAL_MS = 6 * 60 * 60 * 1000; // every 6h while running

mkdirSync(PATHS.SCREENSHOTS_DIR, { recursive: true });

function runCleanup() {
  const purged = purgeOlderThan(retentionDays);
  if (purged > 0) {
    console.log(`[cleanup] purged ${purged} event(s) older than ${retentionDays} days`);
  }
}

let ticking = false;

async function tick() {
  if (ticking) return; // don't overlap if a screenshot+OCR runs long
  ticking = true;
  try {
    const win = await activeWindow();
    const appName = win?.owner?.name ?? null;
    const windowTitle = win?.title ?? null;
    const ts = Date.now();

    const exclusion = win ? filter.checkExclusion(appName, windowTitle) : { excluded: true, reason: 'no-active-window' };
    if (exclusion.excluded) {
      insertEvent({ ts, appName, windowTitle: null, screenshotPath: null, ocrText: null, filtered: true, filterReason: exclusion.reason });
      console.log(`[${new Date(ts).toISOString()}] filtered (${appName ?? 'unknown'}) reason=${exclusion.reason}`);
      return;
    }

    let screenshotPath = null;
    let ocrText = null;

    if (privacyConfig.screenshot_enabled ?? true) {
      const filename = `${ts}.png`;
      screenshotPath = join(PATHS.SCREENSHOTS_DIR, filename);
      await captureScreen(screenshotPath);

      if (privacyConfig.ocr_enabled ?? true) {
        ocrText = filter.redact(await extractText(screenshotPath));
      }
    }

    const redactedTitle = filter.redact(windowTitle);
    insertEvent({
      ts,
      appName,
      windowTitle: redactedTitle,
      screenshotPath,
      ocrText,
      filtered: false,
    });
    // Log the REDACTED title, not the raw one. This line is streamed straight
    // into the GUI's live activity log (MainWindow.OnRecorderOutput ->
    // AppendLog) and to the console/any captured process output - printing
    // the raw title here would leak exactly what redact() exists to hide
    // (card numbers, IDs, emails, phones, secrets in window titles) into a
    // visible UI list and stdout, even though the DB row itself was clean.
    console.log(`[${new Date(ts).toISOString()}] captured (${appName ?? 'unknown'}) ${redactedTitle ?? ''}`);
  } catch (err) {
    console.error('[tick] error:', err.message);
  } finally {
    ticking = false;
  }
}

console.log(`AutoProcess Twin recorder starting. Interval: ${intervalMs}ms. DB: ${PATHS.DB_PATH}`);
console.log(`Retention: ${retentionDays} days (screenshots + DB rows older than that are deleted automatically).`);
console.log('Press Ctrl+C to stop.');

runCleanup();
const timer = setInterval(tick, intervalMs);
const cleanupTimer = setInterval(runCleanup, CLEANUP_INTERVAL_MS);
tick(); // capture one immediately instead of waiting a full interval

function shutdown() {
  console.log('\nStopping recorder...');
  clearInterval(timer);
  clearInterval(cleanupTimer);
  closeDb();
  process.exit(0);
}

process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
