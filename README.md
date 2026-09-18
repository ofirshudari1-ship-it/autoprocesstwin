# AutoProcessTwin

A desktop app that watches how you actually work and points out the repetitive parts worth automating.

## What it does

AutoProcessTwin runs quietly in the background on your Windows PC, keeping track of which app and window you're active in and periodically capturing your screen so it can understand what you're working on. After a few days of use, its "Automation Recommendations" tab surfaces the tasks that repeat over and over — things like CRM data entry, file transfers, or repetitive customer documentation — along with a confidence score, a category breakdown of where your time actually goes, and a trend for each pattern (rising, falling, steady, or new).

**Important: AutoProcessTwin is report-only.** It observes and suggests — it never sends emails, updates your CRM, or takes any action on your behalf. Every recommendation is something you review and decide to act on yourself; nothing runs automatically or autonomously. Recording also never starts on its own — you switch it on each time from the app.

## Download & install

**[Download the latest version](https://github.com/ofirshudari1-ship-it/autoprocesstwin/releases/latest)**

1. Download `AutoProcessTwin-Setup-<version>.exe` from the link above.
2. Run the installer and approve the Windows admin (UAC) prompt — it installs to `C:\Program Files\AutoProcessTwin` for consistency with the rest of the product line.
3. Follow the setup wizard (choose your language, desktop/Start Menu shortcuts, and whether to launch the app right away).
4. Open AutoProcessTwin, review the privacy exclusions (apps/keywords you never want captured), and press Start Recording when you're ready.
5. Done — check back after a few days of normal use to see your first automation recommendations.

**System requirements:** Windows 10 or 11. The installer requires administrator rights (UAC); the app itself runs as a normal user afterward.

## Key features

- Automatic detection of repeating work patterns (CRM entries, file transfers, invoicing, support tickets, and more) across 8 categories
- Confidence score and trend indicator (rising/falling/steady/new) for each detected pattern
- Category time-breakdown showing where the day's tracked time actually goes
- Dismiss/restore for recommendations you've already handled or want to ignore
- Exportable reports in Markdown, HTML, and CSV (Excel-ready, with Hebrew support)
- Optional AI-generated executive summary of your patterns — off by default, only runs if you enable it
- Built-in privacy filter that excludes chosen apps and redacts sensitive text (credit card numbers, IDs) before anything is stored
- Automatic data retention cleanup, system tray integration, and bilingual English/Hebrew interface with dark/light themes

## Automatic updates

AutoProcessTwin checks for new versions automatically and lets you know when one is available, so you don't need to manually track releases. You can also always grab the latest version yourself from the [releases page](https://github.com/ofirshudari1-ship-it/autoprocesstwin/releases).

## Privacy

AutoProcessTwin is local-first: everything it records — window activity, screenshots, and the local database — stays on your own PC and never leaves it by default. The optional AI summary feature is the only thing that can send data off-device, and it is disabled out of the box; it only activates if you turn it on and provide your own API key. You also control what's excluded from capture entirely (specific apps or keywords), and old data is cleaned up automatically based on a retention period you set.
