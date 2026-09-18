import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { homedir } from 'node:os';

const __dirname = dirname(fileURLToPath(import.meta.url));
// ROOT = install root (agent/ + config/*.example.json ship here). Since
// 0.5.1 this can be C:\Program Files\AutoProcessTwin, which is read-only
// for a non-elevated user after install — so only the shipped *.example.json
// templates live here. Real, writable config/data/reports live under
// %LOCALAPPDATA%\AutoProcessTwin instead (mirrors AppPaths.cs on the C# side,
// which resolves the very same physical folder independently).
const ROOT = join(__dirname, '..', '..');
const EXAMPLE_CONFIG_DIR = join(ROOT, 'config');

const LOCALAPPDATA = process.env.LOCALAPPDATA || join(homedir(), 'AppData', 'Local');
const USER_ROOT = join(LOCALAPPDATA, 'AutoProcessTwin');
const CONFIG_DIR = join(USER_ROOT, 'config');

function loadJson(name) {
  const real = join(CONFIG_DIR, `${name}.json`);
  const example = join(EXAMPLE_CONFIG_DIR, `${name}.example.json`);
  const path = existsSync(real) ? real : example;
  if (!existsSync(path)) {
    throw new Error(`Missing config: ${real} (and no ${example} fallback)`);
  }
  return { data: JSON.parse(readFileSync(path, 'utf-8')), usingExample: path === example };
}

export function loadPrivacyConfig() {
  const { data, usingExample } = loadJson('privacy');
  if (usingExample) {
    console.warn('[config] config/privacy.json not found — running on privacy.example.json defaults. Copy it and adjust before real use.');
  }
  return data;
}

export function loadGuardrailsConfig() {
  const { data, usingExample } = loadJson('guardrails');
  if (usingExample) {
    console.warn('[config] config/guardrails.json not found — running on guardrails.example.json defaults.');
  }
  return data;
}

export function loadAiConfig() {
  const { data, usingExample } = loadJson('ai');
  if (usingExample) {
    console.warn('[config] config/ai.json not found — running on ai.example.json defaults (AI insights disabled).');
  }
  return data;
}

export const PATHS = {
  ROOT: USER_ROOT,
  INSTALL_ROOT: ROOT,
  DATA_DIR: join(USER_ROOT, 'data'),
  SCREENSHOTS_DIR: join(USER_ROOT, 'data', 'screenshots'),
  DB_PATH: join(USER_ROOT, 'data', 'twin.db'),
  REPORTS_DIR: join(USER_ROOT, 'reports'),
};
