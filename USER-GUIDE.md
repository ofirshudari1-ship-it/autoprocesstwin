# AutoProcess Twin — User Guide

## What It Does

AutoProcess Twin records which applications you use throughout the day, identifies patterns in repeated actions, and generates a daily morning briefing showing where your time went. Everything runs locally — nothing is sent to the cloud.

**Phase 1 (current):** Activity recording + daily briefing + repetitive-action detection with automation suggestions.

## Quick Start

1. **Install:** Run `AutoProcessTwin-Setup-0.5.1.exe` (installs to `Program Files` for all users — requires an admin/UAC prompt; your recordings, config and reports stay per-user under `AppData\Local`)
2. **Launch:** Double-click the desktop shortcut or find it in Start Menu
3. **Start Recording:** Click the **▶ Start Recording** button
4. **Stop at end of day:** Click **⏹ Stop Recording**, or let it run in the background

The app minimizes to the system tray when you close the window (`×`). To exit fully, right-click the tray icon → **Exit**.

## Tabs

| Tab | Purpose |
|-----|---------|
| **Home** | Dashboard: observation count, live log, Start/Stop recording |
| **Patterns** | Detected repeated actions and automation suggestions |
| **Settings** | Language, theme, startup behavior, data folder |
| **Privacy** | Excluded apps, observation interval |
| **Guardrails** | Action limits (for future AI phase) |
| **Help / About** | Docs, version info, open data folders |

## Privacy & What Gets Recorded

- **Active window title** — app name and window title (e.g., "Chrome - Gmail")
- **Screenshot** — taken every N seconds (configurable, default: 30s)
- **OCR text** from screenshot — for context in the briefing
- **Nothing else** — no keystrokes, no audio, no mouse position

**Excluded apps** (default): password managers, banking apps, 1Password, KeePass, and others. Edit in the **Privacy** tab.

## Morning Briefing

Click **Generate Morning Briefing** on the Home tab. A markdown report opens showing:
- Time breakdown per application
- Repeated sequences detected
- What was filtered (excluded apps)

Reports are saved to `%APPDATA%\AutoProcessTwin\reports\` (or the configured data folder).

## System Requirements

- **Windows 10 / 11** (64-bit)
- **.NET Framework 4.8** (pre-installed on Windows 10 1903+ and Windows 11)
- **Node.js 18+** — required for the recording agent ([nodejs.org](https://nodejs.org))

## Data Location

| Item | Location |
|------|---------|
| Config | `%AppData%\AutoProcessTwin\config\` |
| Data / logs | `%AppData%\AutoProcessTwin\data\` |
| Reports | `%AppData%\AutoProcessTwin\reports\` |

## Uninstall

Settings → Apps → AutoProcess Twin → Uninstall  
(or run the Setup again — it will detect the existing install and offer to remove it)

Your data folder (`config\`, `data\`, `reports\`) is **not** deleted on uninstall. Delete it manually from `%AppData%\AutoProcessTwin\` if you want a clean removal.

## Frequently Asked Questions

**Q: The app disappeared — where did it go?**  
A: It's in the system tray (bottom-right corner). Click the `^` arrow if you don't see the icon, then click the AutoProcess Twin icon to reopen.

**Q: Can I pause recording without closing the app?**  
A: Yes — click **⏹ Stop Recording**. The app keeps running in the tray, ready to start again.

**Q: Is there a way to add my own excluded apps?**  
A: Yes — go to the **Privacy** tab and add app names to the exclusion list.

**Q: Does this need internet access?**  
A: No. Everything is local. The only exception is if you enable the optional AI briefing feature (Settings → AI tab), which sends aggregated stats (never raw screenshots or text) to an AI service you configure.
