// Small JSON status query for the GUI app (avoids needing a SQLite driver in
// C# - the GUI just shells out to `node status.js` and parses stdout as JSON).
import { eventsBetween, closeDb } from './db.js';

function startOfDay(date) {
  const d = new Date(date);
  d.setHours(0, 0, 0, 0);
  return d.getTime();
}

const dayStart = startOfDay(new Date());
const dayEnd = dayStart + 24 * 60 * 60 * 1000;
const events = eventsBetween(dayStart, dayEnd);

const captured = events.filter((e) => !e.filtered);
const filtered = events.filter((e) => e.filtered);
const last = events.length > 0 ? events[events.length - 1] : null;

process.stdout.write(JSON.stringify({
  eventsToday: events.length,
  capturedToday: captured.length,
  filteredToday: filtered.length,
  lastApp: last ? last.app_name : null,
  lastTs: last ? last.ts : null,
  lastWasFiltered: last ? !!last.filtered : null,
}));

closeDb();
