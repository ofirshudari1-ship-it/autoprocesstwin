// OCR & Vision Context Engine (spec section 2) — best-effort wrapper around the
// `tesseract` CLI. If it isn't installed, OCR is silently skipped: screenshots
// are still saved, just without extracted text. See README "מה חסר" section.

import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { existsSync } from 'node:fs';

const execFileAsync = promisify(execFile);

// A freshly-installed tesseract adds itself to PATH, but already-running
// processes (including whatever launched this one, e.g. an Explorer shortcut)
// don't see an updated PATH until they restart. Fall back to the well-known
// default install location so OCR works immediately after install, not just
// after a reboot/relogin.
const FALLBACK_PATHS = [
  'C:\\Program Files\\Tesseract-OCR\\tesseract.exe',
  'C:\\Program Files (x86)\\Tesseract-OCR\\tesseract.exe',
];

let resolvedPath = null; // null = not checked yet, false = not available, string = path/command to use
let warned = false;

async function resolveTesseract() {
  if (resolvedPath !== null) return resolvedPath;

  try {
    await execFileAsync('tesseract', ['--version']);
    resolvedPath = 'tesseract';
    return resolvedPath;
  } catch {
    // not on PATH — try known install locations directly
  }

  for (const candidate of FALLBACK_PATHS) {
    if (existsSync(candidate)) {
      resolvedPath = candidate;
      return resolvedPath;
    }
  }

  resolvedPath = false;
  if (!warned) {
    warned = true;
    console.warn('[ocr] tesseract not found on PATH or in the default install location — screenshots will be saved without OCR text. Install: https://github.com/UB-Mannheim/tesseract/wiki');
  }
  return false;
}

export async function extractText(imagePath) {
  const exe = await resolveTesseract();
  if (!exe) return null;
  try {
    const { stdout } = await execFileAsync(exe, [imagePath, 'stdout', '-l', 'heb+eng']);
    return stdout.trim() || null;
  } catch (err) {
    console.warn('[ocr] extraction failed:', err.message);
    return null;
  }
}
