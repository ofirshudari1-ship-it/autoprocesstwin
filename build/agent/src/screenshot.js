// Thin wrapper around screenshot.ps1 (System.Windows.Forms + System.Drawing).
// Written in-house instead of relying on the `screenshot-desktop` npm package,
// which shells out to a legacy self-compiling .bat/csc.exe hybrid that failed
// silently on this machine. This is a handful of lines we can actually debug.

import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const execFileAsync = promisify(execFile);
const __dirname = dirname(fileURLToPath(import.meta.url));
const SCRIPT_PATH = join(__dirname, 'screenshot.ps1');

export async function captureScreen(outPath) {
  await execFileAsync('powershell', [
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', SCRIPT_PATH,
    '-OutPath', outPath,
  ]);
  return outPath;
}
