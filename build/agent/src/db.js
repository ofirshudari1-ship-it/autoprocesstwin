import { DatabaseSync } from 'node:sqlite';
import { mkdirSync, unlinkSync } from 'node:fs';
import { dirname } from 'node:path';
import { PATHS } from './config.js';

let db;

export function openDb() {
  if (db) return db;
  mkdirSync(dirname(PATHS.DB_PATH), { recursive: true });
  db = new DatabaseSync(PATHS.DB_PATH);
  db.exec(`
    CREATE TABLE IF NOT EXISTS activity_events (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      ts INTEGER NOT NULL,
      app_name TEXT,
      window_title TEXT,
      screenshot_path TEXT,
      ocr_text TEXT,
      filtered INTEGER NOT NULL DEFAULT 0
    );
    CREATE INDEX IF NOT EXISTS idx_activity_events_ts ON activity_events(ts);
  `);
  // מיגרציה בטוחה עבור DB שנוצר לפני שנוסף filter_reason (גרסאות קודמות
  // של האפליקציה) - ALTER TABLE נכשל בשקט אם העמודה כבר קיימת.
  try {
    db.exec(`ALTER TABLE activity_events ADD COLUMN filter_reason TEXT`);
  } catch {
    // כבר קיימת - בסדר
  }
  return db;
}

export function insertEvent({ ts, appName, windowTitle, screenshotPath, ocrText, filtered, filterReason }) {
  const database = openDb();
  const stmt = database.prepare(`
    INSERT INTO activity_events (ts, app_name, window_title, screenshot_path, ocr_text, filtered, filter_reason)
    VALUES (?, ?, ?, ?, ?, ?, ?)
  `);
  stmt.run(ts, appName ?? null, windowTitle ?? null, screenshotPath ?? null, ocrText ?? null, filtered ? 1 : 0, filterReason ?? null);
}

export function eventsBetween(startTs, endTs) {
  const database = openDb();
  const stmt = database.prepare(`
    SELECT * FROM activity_events WHERE ts >= ? AND ts < ? ORDER BY ts ASC
  `);
  return stmt.all(startTs, endTs);
}

// Deletes screenshot files + DB rows older than retentionDays. Screenshots are
// taken every capture_interval_ms forever, so without this a machine left
// running for months slowly fills its disk. Returns how many rows were purged.
export function purgeOlderThan(retentionDays) {
  const database = openDb();
  const cutoff = Date.now() - retentionDays * 24 * 60 * 60 * 1000;

  const oldRows = database.prepare(
    'SELECT id, screenshot_path FROM activity_events WHERE ts < ?'
  ).all(cutoff);

  for (const row of oldRows) {
    if (row.screenshot_path) {
      try {
        unlinkSync(row.screenshot_path);
      } catch {
        // already gone or never existed — fine, still purge the DB row below
      }
    }
  }

  database.prepare('DELETE FROM activity_events WHERE ts < ?').run(cutoff);
  return oldRows.length;
}

export function closeDb() {
  if (db) {
    db.close();
    db = undefined;
  }
}
