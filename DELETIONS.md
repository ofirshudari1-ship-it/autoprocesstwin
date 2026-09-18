# Deletions Log

Per STANDARDS.md hard rule: every file deletion is logged here with a reason
before it happens, unless it's unambiguously reproducible build output.

## 2026-09-15

- **`AutoProcessTwin-Setup-0.3.0.exe`** (root) — superseded installer from the
  previous version. `AutoProcessTwin-Setup-0.4.0.exe` was rebuilt from current
  source via `build/installer/build-installer.cmd` and verified (`--selftest`
  full pass on the packaged `build/dist/AutoProcessTwin/AutoProcessTwin.exe`).
  Deleting the old one avoids the "two installer files in root" duplicate
  STANDARDS.md explicitly forbids (§1, §10). Fully reproducible from source —
  no unique content lost.

## 2026-09-16

- **`AutoProcessTwin-Setup-0.4.0.exe`** (root) — superseded by
  `AutoProcessTwin-Setup-0.5.0.exe`, rebuilt from current source via
  `build/installer/build-installer.cmd` after the product-improvement pass
  (confidence score, category breakdown, CSV export, privacy-log fix — see
  CHANGELOG.md `[0.5.0]`) and verified (`AutoProcessTwin.exe --selftest` ->
  `=== ALL PASS ===`, exit code 0: 3 C#-level checks + 39 Node-level checks,
  on the packaged `build/dist/AutoProcessTwin/AutoProcessTwin.exe`). Fully
  reproducible from source — no unique content lost.

## 2026-09-17

- **`AutoProcessTwin-Setup-0.5.0.exe`** (root) — superseded by
  `AutoProcessTwin-Setup-0.5.1.exe`, rebuilt from current source (manual
  build steps equivalent to `build/installer/build-installer.cmd` — see
  CHANGELOG.md `[0.5.1]` for the Program Files / UAC / HKLM / data-path
  change this packages) after clearing stale dev-run `data/`+`reports/`
  that had accumulated under `build/dist/AutoProcessTwin/` from earlier
  manual `--selftest` runs (those aren't part of the shipped payload —
  their presence in the old 0.5.0 zip, ~4.9MB vs 0.5.1's ~2.5MB, was
  itself a latent bug: real recorded screenshots/DB rows were being
  bundled into the installer). Verified
  (`AutoProcessTwin.exe --selftest` -> `=== ALL PASS ===`, exit code 0,
  on the packaged `build/dist/AutoProcessTwin/AutoProcessTwin.exe`, and
  confirmed the DB/report paths resolved to
  `%LocalAppData%\AutoProcessTwin\...` as expected). Fully reproducible
  from source — no unique content lost.
