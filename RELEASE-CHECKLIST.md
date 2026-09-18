# AutoProcess Twin — Release Checklist

Use this checklist before publishing any release. Each item must be checked manually.

## Pre-release

- [ ] `version.json` updated to new version
- [ ] `CHANGELOG.md` updated (Keep a Changelog format, new version section added)
- [ ] `build\installer\Setup.cs` — `AppVersion` constant matches `version.json`
- [ ] `build\app\src\Strings.cs` — `Version` property matches new version
- [ ] All strings in `build\locales\en.json` and `he.json` are in sync with `Strings.cs`

## Build

- [ ] Run `build\app\build.cmd` — 0 errors, 0 warnings
- [ ] Run `build\installer\build-installer.cmd` — produces `AutoProcessTwin-Setup-X.Y.Z.exe`
- [ ] Selftest passes: run setup with `--silent-install` or run selftest.cmd: 32/32 PASS

## Functional Smoke Test

- [ ] Fresh install: run the installer, app launches after "Finish"
- [ ] Onboarding wizard appears on first run
- [ ] Splash screen appears for ≥1.5 seconds, then main window
- [ ] Start Recording → status indicator turns green, observation counter increments
- [ ] Stop Recording → status returns to idle
- [ ] Minimize via `×` → app moves to tray, balloon tip appears
- [ ] Tray icon right-click menu works (Open / Start-Stop Rec / Exit)
- [ ] Exit via tray menu → app closes fully
- [ ] Window position is restored after reopen
- [ ] Language toggle (Settings) → UI switches between EN/HE correctly
- [ ] Morning briefing generation (requires at least 1 observation)

## Privacy

- [ ] Excluded apps list is respected (screenshot not taken for excluded app)
- [ ] "Filtered" appears in log for excluded app (no content stored)
- [ ] Privacy tab shows correct excluded list

## Installer

- [ ] Installer header shows correct version
- [ ] Language toggle in installer works (HE/EN)
- [ ] UAC prompt appears during install (Program Files path, since 0.5.1) — installer relaunches itself elevated if not started as admin
- [ ] Desktop and Start Menu shortcuts created when checked
- [ ] "Start with Windows" option registers correctly
- [ ] Uninstall entry appears in Settings → Apps with correct version (HKLM, since 0.5.1)
- [ ] Uninstall does NOT delete user data folder (`%LocalAppData%\AutoProcessTwin`)
- [ ] Upgrading from a pre-0.5.1 AppData install: old config/data/reports show up under `%LocalAppData%\AutoProcessTwin` after install, old AppData install dir and HKCU registry entry are gone

## Documentation

- [ ] `README.md` mentions new features / changes
- [ ] `USER-GUIDE.md` is up to date
- [ ] `SPEC.md` updated if behavior changed

## Cleanup

- [ ] No old `Setup-AutoProcessTwin.exe` at root
- [ ] No `dist\` artifacts committed (should be in `.gitignore`)
- [ ] No duplicate source files
- [ ] `STANDARDS.md` status table updated for this product

## After Release

- [ ] Tag the version in git: `git tag v0.X.Y`
- [ ] Update `_AUDIT\STANDARDS.md` status table row for AutoProcessTwin
