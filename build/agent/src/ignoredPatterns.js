// Small persisted list of pattern keys the user has explicitly dismissed
// ("already automated this, stop showing it" / "not relevant"). Deliberately
// separate from privacy.json/guardrails.json - this isn't config, it's a
// growing user decision log, simplest as its own flat file.
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { PATHS } from './config.js';
import { join } from 'node:path';

const FILE_PATH = join(PATHS.ROOT, 'config', 'ignored-patterns.json');

export function loadIgnoredPatterns() {
  if (!existsSync(FILE_PATH)) return [];
  try {
    const data = JSON.parse(readFileSync(FILE_PATH, 'utf-8'));
    return Array.isArray(data) ? data : [];
  } catch {
    return [];
  }
}

export function addIgnoredPattern(key) {
  const current = loadIgnoredPatterns();
  if (!current.includes(key)) {
    current.push(key);
    mkdirSync(dirname(FILE_PATH), { recursive: true });
    writeFileSync(FILE_PATH, JSON.stringify(current, null, 2), 'utf-8');
  }
  return current;
}

export function removeIgnoredPattern(key) {
  const current = loadIgnoredPatterns().filter((k) => k !== key);
  mkdirSync(dirname(FILE_PATH), { recursive: true });
  writeFileSync(FILE_PATH, JSON.stringify(current, null, 2), 'utf-8');
  return current;
}
