# Changelog

All notable changes to AutoProcessTwin are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)

## [0.5.3] - 2026-09-18

Distribution moves to GitHub: the project now has a public repo at
https://github.com/ofirshudari1-ship-it/autoprocesstwin, which is the
canonical source and Releases page going forward (installers are published
there, not just handed off locally).

### Added
- **Automatic update checking** (`build/app/src/UpdateChecker.cs`, wired into
  `MainWindow.cs`): on launch, after a 5-second delay and once per session,
  the app makes a single unauthenticated `GET` to
  `https://api.github.com/repos/ofirshudari1-ship-it/autoprocesstwin/releases/latest`
  (with a `User-Agent` header, as GitHub's REST API requires - no token, ever,
  since the repo is public and this is meant to ship inside the app). The
  returned `tag_name` is compared against the running version with a small
  `major.minor.patch` semver check. If a newer release exists, the app shows
  a non-blocking notification (tray balloon tip + a line in the "עזרה"/Help
  tab) that opens the GitHub release page on click - it never downloads or
  installs anything automatically. Any failure (offline, GitHub rate-limited,
  no releases yet) is swallowed silently; the check never blocks the UI
  thread and never interrupts the user. Opt-out via a new "בדוק עדכונים
  אוטומטית" / "Automatically check for updates" checkbox in General Settings
  (`app.json["check_for_updates"]`, default `true`); a "בדוק עדכונים עכשיו" /
  "Check for Updates Now" button in the Help tab triggers the same check
  on demand, bypassing the once-per-session limit.
- `build/config/app.example.json`: new `check_for_updates` default (`true`).

### Changed
- Version bumped to 0.5.3 (`version.json`, `MainWindow.AppVersion`,
  `Setup.cs AppVersion`, `build-installer.cmd` output filename). No
  installer `.exe` was rebuilt for this release - see RELEASE-CHECKLIST.md
  for that step.
- `.gitignore` tightened for the new public repo: generated build output
  (`build/dist/`), .NET build artifacts (`bin/`, `obj/`, `.vs/`), the
  root-level `AutoProcessTwin-Setup-*.exe` installers (shipped as GitHub
  Release assets, never committed), and `build/config/*.json` /
  `build/reports/*.md`/`.html` (real user config and generated reports,
  mirroring the existing root-level rules) are now excluded.

## [0.5.2] - 2026-09-17

Installer visual polish only, following the 0.5.1 Program Files/UAC change -
no install-location or elevation logic touched here.

### Changed
- **Themed checkboxes and progress bar in the installer** (`build/installer/Setup.cs`):
  the four setup checkboxes (desktop/Start Menu/start-with-Windows/launch-now)
  and the "Installing..." progress indicator previously rendered with native
  Windows visual styles (a plain white checkbox square, a system-blue marquee
  bar) regardless of the form's dark background - the one part of the
  installer that still looked like generic WinForms chrome inside an
  otherwise fully dark-slate/emerald themed window. Replaced with
  `ThemedCheckBox` and `ThemedProgressBar`, two small owner-drawn controls
  using the exact same palette as `Theme.cs`/`Setup.cs`'s `Theme` class
  (`#10B981` accent, `#273349` panel, `#94A3B8` muted) and the same rounded-
  corner language as the buttons and cards.
- Confirmed (not modified) that the header/logo banner, button colors,
  panel backgrounds and text colors in `Setup.cs` already matched
  `Theme.cs`'s dark-slate + emerald palette exactly - `assets/BRAND.md` was
  corrected on 2026-09-16 to document this (a stale purple `#6450DC` entry
  in that doc was never actually implemented anywhere in the shipped
  product; verified again here before touching any color).

### Verified, not changed
- Program Files install directory / UAC elevation (`Setup.cs`,
  `AppPaths.cs`, `Program.cs`) from 0.5.1 is untouched.
- Bilingual EN/HE UI, desktop/Start Menu shortcuts, HKLM uninstall
  registration and `--self-uninstall` all still present.

## [0.5.1] - 2026-09-17

Owner decision (Ofir Shudari, explicit and direct) overriding the 0.4.0
"why AppData, not Program Files" decision documented in `README.md`: **every
tool in the portfolio defaults to installing under `C:\Program Files`**,
for consistency across the whole product line. This release implements that
for AutoProcessTwin specifically — packaging/install-location only, no new
features or capture behavior.

### Changed
- **Installer default install directory** is now `C:\Program
  Files\AutoProcessTwin` (was `%LocalAppData%\Programs\AutoProcessTwin`)
  (`build/installer/Setup.cs: SetupForm.DefaultInstallDir`).
- **Installer requests admin rights (UAC)**: `Program.Main` checks
  `WindowsPrincipal.IsInRole(Administrator)` and relaunches itself with the
  `runas` verb if not elevated (same pattern as the OptiGuard installer).
  `--silent-install` does **not** self-elevate (silent = no user interaction,
  and a UAC prompt is interaction) — it fails loudly with the underlying
  `UnauthorizedAccessException` message if not already run elevated.
- **Uninstall registry entry moved from `HKCU` to `HKLM`**
  (`SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\AutoProcessTwin`) to
  match the per-machine Program Files install
  (`build/installer/Setup.cs: RegisterUninstall`).
- **`AutoProcessTwin.exe --self-uninstall` self-elevates** if not already
  admin (needed to delete the Program Files folder and the new HKLM key) —
  `build/app/src/Program.cs`.
- **The running app itself is *not* elevated** — `build/app/app.manifest`
  keeps `requestedExecutionLevel="asInvoker"`. The 0.4.0 reasoning (the
  recorder needs the interactive user session, not elevation) still holds;
  only the *installer's* target directory changed, per explicit owner
  direction that overrides the install-location call specifically, not the
  app's own runtime privilege level.

### Fixed (the actual bug this location change would have caused)
- **Writable data moved out of the install directory.** Program Files is
  read-only to a non-elevated user after install, but the app previously
  read/wrote `config/*.json` (privacy, guardrails, ai, app), `data/twin.db`
  + screenshots, and `reports/` all directly next to the exe
  (`AppPaths.Root` / Node's `ROOT` in `agent/src/config.js`). Left unfixed,
  every install to Program Files would have installed successfully and then
  failed on first use (recording can't create `data/twin.db`, settings can't
  save) — a much worse failure mode than a UAC prompt.
  - `build/app/src/AppPaths.cs`: added `UserDataRoot` =
    `%LocalAppData%\AutoProcessTwin`; `ConfigDir`/`DataDir`/`ReportsDir` now
    point there. `Root` still resolves the *read-only* payload location
    (`AgentDir`, the `*.example.json` templates) — `*ExamplePath` properties
    now explicitly read from `Root\config`, not `ConfigDir`.
  - `build/agent/src/config.js`: `CONFIG_DIR`/`PATHS.DATA_DIR`/
    `PATHS.SCREENSHOTS_DIR`/`PATHS.DB_PATH`/`PATHS.REPORTS_DIR` now resolve
    under `process.env.LOCALAPPDATA` + `AutoProcessTwin`, independently of
    the C# side (both land on the same physical folder without any IPC/env
    var handoff between the two processes). `*.example.json` templates still
    read from the install-root `config/` dir (`EXAMPLE_CONFIG_DIR`).
- **One-time legacy-data migration** for anyone upgrading from ≤0.5.0 (where
  data lived next to the AppData install): the installer, on detecting an
  old `HKCU` uninstall entry, copies `config/data/reports` from the old
  install dir into `%LocalAppData%\AutoProcessTwin` **before** deleting the
  old install dir, then removes the old `HKCU` key, the old `Run` startup
  entry, and the old install folder
  (`build/installer/Setup.cs: CleanupLegacyInstall`). `AppPaths.
  MigrateLegacyDataIfNeeded()` on the app side is a second, idempotent
  safety net for the case where the exe was copied manually instead of run
  through the installer.

### Known gaps / not verified live
- No Windows GUI was available in this session — the elevation prompt, the
  actual Program Files write, the HKLM registration, and the legacy-data
  migration were all verified by code review and a clean build only, **not**
  by running the installer end-to-end as a real elevated install. See
  `RELEASE-CHECKLIST.md` "Installer" section for the manual checks still
  needed before shipping.

## [0.5.0] - 2026-09-16

Competitor research pass (RescueTime, Timely, Rewind AI/Screenpipe, Microsoft
Viva Insights — see sources at the end of this entry) to check what a
report-only tool like this one is missing compared to established time-mining
products, staying strictly inside the "report/recommend only, never act"
boundary the product deliberately keeps (SPEC.md "עדכון אפיון"). Two gaps
stood out as directly applicable without crossing into autonomous-action
territory: (1) RescueTime/Timely both attach a confidence/certainty signal to
automated suggestions rather than presenting every detection with equal
weight: (2) Viva Insights' manager view and Timely's "Enhanced Client/Project
tables" both lead with a category/time-allocation rollup, not just a flat
list of individual detections — which is also literally what SPEC.md asks for
("בדשבורד רואים את כל הנתונים ומנתחים כל חודש כמה עובד מבזבז זמן ואיפה").

### Added
- **Confidence score** per detected pattern (`patterns.js: computeConfidence`)
  — a 0-100 score + high/medium/low label combining day-coverage and
  occurrence-density, shown only on automation candidates (not on background
  noise, to avoid implying false precision). Explicitly documented in the
  code and in every place it's surfaced as "not an AI/ML score, two simple
  ratios" — same honesty bar as the existing keyword-category rules. Shown as
  a badge on each pattern card in the GUI (`MainWindow.cs: BuildConfidenceBadge`,
  wired into `BuildPatternCard`), and in the markdown/HTML/CSV exports
  (`patterns-report.js`).
- **Category time-breakdown** (`patterns.js: analyzePatterns` →
  `categoryBreakdown`) — aggregates recorded minutes by category (CRM,
  invoicing, support, files, spreadsheets, mail, chat, meetings, "not
  categorized") across *all* patterns, not just automation candidates, so the
  percentages reconcile with total tracked time. This is the direct "where
  does the time go this month" view the product spec calls for, distinct from
  the per-pattern list which answers "what specifically repeats". Rendered as
  a bar-chart-style panel at the top of the "פעולות חוזרות" tab
  (`MainWindow.cs: RenderCategoryBreakdown`) and in the markdown/HTML/CSV reports.
- **CSV export** of automation candidates (`patterns-report.js: toCsv`) with
  UTF-8 BOM for correct Hebrew rendering in Excel — the format a manager
  actually pivots/filters in, alongside the existing read-once HTML/markdown.
- Report-only, explicitly: none of the above triggers, schedules, or performs
  any action. They only change what's *displayed* about data the recorder
  already collected. No new capture capability, no autonomous behavior added.

### Fixed
- **Privacy filter leak in the live activity log**: `agent/src/index.js`'s
  captured-event log line printed the *raw, unredacted* window title (e.g.
  `${windowTitle}`) to stdout, which streams straight into the GUI's visible
  "יומן פעילות חי" list (`MainWindow.cs: OnRecorderOutput` → `AppendLog`) and
  to any captured process output. The SQLite row itself was correctly
  redacted (`filter.redact(windowTitle)`), but the console/GUI-visible copy
  was not — meaning a credit-card number, ID, email, phone, or secret caught
  by `privacyFilter.js: redact()` could still show up in plain sight in the
  app's own log, defeating the point of redacting it. Fixed by logging the
  already-redacted title instead of the raw one.

### Changed
- Version bumped to 0.5.0 across `version.json`, `build/installer/Setup.cs`
  (`AppVersion`), `build/app/src/MainWindow.cs` (`AppVersion` const, About
  tab), `build/app/app.manifest`, `build/installer/build-installer.cmd`
  output filename, `USER-GUIDE.md`.
- `agent/src/selftest.js`: added 6 checks for the new confidence score and
  category breakdown output (numeric range, label set, sort order, and that
  non-candidates correctly have no confidence score) — 39 checks total, all
  passing (`node agent/src/selftest.js`).

### Sources (competitor research, 2026-09-16)
- RescueTime 2026 update / Daily Focus Coach — github.com/ever-works/awesome-time-tracking
- Timely 2026 AI Timesheet Assistant / custom reporting dashboards — timely.com/timely-features, macroter.com/timely-app
- Screenpipe as the Rewind AI successor (Rewind discontinued Dec 2025 post-Meta
  acquisition) — screenpi.pe/blog/rewind-ai-alternative-2026
- Microsoft Viva Insights manager dashboard / time-management features, and
  its acknowledged gap (no timesheet/project-allocation data, generic
  suggestions) — support.microsoft.com/en-us/viva/insights, reclaim.ai/blog/microsoft-viva-insights-alternatives

## [0.4.0] - 2026-09-15

### Added
- Landing page `site/index.html` (STANDARDS §17) — bilingual (English default,
  Hebrew toggle with full RTL), hero/how-it-works/features/FAQ/privacy/footer
  sections, honest privacy disclosure matching the app's actual data handling
  (what's recorded, what the optional AI summary sends, retention behavior)

### Changed
- Version bumped to 0.4.0 across `version.json`, `build/installer/Setup.cs`
  (`AppVersion`), `build/app/src/MainWindow.cs` (About tab), `build/app/app.manifest`,
  `build/installer/build-installer.cmd` output filename, `USER-GUIDE.md`
- Root folder cleanup (STANDARDS §1): moved `FINDINGS.md` → `build/FINDINGS.md`
  and the stray dev-run `reports/*.md`/`*.html` output → `build/reports/`
  (regenerated automatically next time the app runs from this checkout;
  `data/` stays in root — explicitly permitted by STANDARDS §1 as project-local
  tool data, and already `.gitignore`d)
- README.md: added an explicit "why AppData, not Program Files" section
  documenting the privilege-model reasoning (recorder needs the interactive
  user session, not elevation — Program Files would add a UAC prompt with no
  functional benefit); updated internal doc links after the FINDINGS.md move

### Known issues (documented honestly, not silently left broken)
- `build/locales/en.json` / `he.json` exist (STANDARDS §4 file convention) but
  are **not actually loaded at runtime** — `Strings.cs` holds every UI string
  as a hardcoded C#/ternary lookup (`IsHe ? "..." : "..."`), which does give
  full bilingual coverage with no hardcoded-in-the-wrong-language strings, but
  isn't the external-JSON-file architecture the standard describes. Left as-is
  this round rather than claiming a fix that wasn't done — rewiring `Strings.cs`
  to load from the JSON files is a real but non-trivial refactor (every string
  becomes a dictionary lookup with a startup-load step) that risks regressions
  across ~30 UI surfaces for a purely architectural (not user-visible) gap.
- No live Windows GUI session was available to visually verify this round's
  changes (site page rendering aside, which was checked in the browser preview
  pane) — DPI scaling, onboarding, and tray behavior were not re-verified
  visually; they rely on the visual verification already recorded in
  `build/FINDINGS.md` from prior rounds and were not touched by this round's
  code changes.

## [0.3.0] - 2026-09-14

### Added
- Splash screen (1.5 s minimum, dark-theme, fade-in) before main window (STANDARDS §5)
- System tray integration: `×` minimizes to tray; tray right-click menu for Open / Start-Stop Recording / Exit (STANDARDS §12)
- Window state persistence: position, size, maximized state saved to app config and restored on next launch (STANDARDS §12.3)
- Bilingual installer (English / Hebrew) with language-toggle button; default is English
- Crash logger: unhandled exceptions written to `%APPDATA%\AutoProcessTwin\logs\crash.log`
- Installer pre-launch check: warns and offers retry/cancel if AutoProcessTwin is already running
- Firewall rule added during install (best-effort, no UAC required) (STANDARDS §11.8)
- DPI awareness manifest (PerMonitorV2) for crisp rendering on HiDPI monitors (STANDARDS §14.2)
- Locale files `build/locales/en.json` and `he.json` with all UI strings (STANDARDS §4)
- Brand documentation `assets/BRAND.md` with color tokens, typography, and voice guidelines (STANDARDS §3)
- User guide `USER-GUIDE.md` with quick-start, tab reference, FAQ (STANDARDS §9)
- Release checklist `RELEASE-CHECKLIST.md` for pre-release validation (STANDARDS §9)
- 30+ new strings in `Strings.cs`: tray, about, splash, onboarding, footer, header
- Clean root layout: all build sources moved to `build/`; root contains only installer, docs, and data

### Changed
- Default UI language changed from Hebrew to English (STANDARDS §4 — English default for all tools)
- Installer `RightToLeft` layout now follows the selected language instead of being hardcoded
- Installer output renamed to `AutoProcessTwin-Setup-<version>.exe` at project root
- OnboardingWizard: title, buttons, and flow direction are now fully localized (no Hebrew literals)
- About tab: copyright (© 2026), description, and all button labels use `Strings.*` (no literals)
- Header subtitle and footer text use `Strings.*` (no Hebrew literals)

### Fixed
- App crash/disappear on first launch: `ConfigStore.LoadWithFallback` now returns an empty dict when both config files are missing, preventing a `FileNotFoundException` before any window appeared
- Constructor crash protection: `MainWindow()` wraps config load in try/catch so a broken config never prevents the window from opening
- Installer `Loc` class rewritten with C# 5–compatible static constructor (`Add()` calls) — C# 6 index-initializer syntax caused a compile error on the raw `csc.exe` toolchain
- `_recorder.Stopped` handler uses single-argument lambda (matches `Action<int>` signature)

## [0.2.0] - 2026-09-10

### Added
- Single-instance Mutex (`Global\AutoProcessTwin-SingleInstance-9F3C2A`)
- Selftest mode: `AutoProcessTwin.exe --selftest` (32 tests)
- DPAPI encryption for stored API keys
- Node.js agent subprocess with environment-variable key injection

### Changed
- Migrated source layout from flat `app/` to `build/app/`, `build/agent/`, `build/installer/`

## [0.1.0] - 2026-09-01

### Added
- Initial WPF desktop app skeleton
- Node.js ESM agent (`node:sqlite`, built-in Node 24)
- ConfigStore with `app.example.json` and `agent.example.json` fallbacks
- Dark / Light theme support
- Hebrew / English localization (`Strings.cs`)
