# AutoProcess Twin — Brand Identity

## Identity

**Product Name:** AutoProcess Twin  
**Tagline (EN):** Your automation assistant + digital twin  
**Tagline (HE):** סייען אוטומציה + כפיל דיגיטלי  
**Short Name:** APT

## Color Palette

**Note (2026-09-16):** this table previously documented a purple palette
(`#6450DC`/`#4A38B0`) that was never actually implemented — `Theme.cs` (the
real WPF UI), the exported HTML/CSV reports (`patterns-report.js`), and the
landing page have all consistently used the dark-slate + emerald palette
below since at least v0.3.0. Updated the doc to match the shipped product
instead of leaving stale, never-built values on record.

| Token | Hex | Usage |
|-------|-----|-------|
| Primary (Accent) | `#10B981` | Accent, buttons, progress, active/recording state |
| Primary Hover | `#34D399` (dark) / `#059669` (light) | Hover/pressed state |
| BG Dark | `#0F172A` | Main background (dark theme) |
| BG Light | `#F1F5F9` | Main background (light theme) |
| Surface Dark | `#1E293B` | Cards, panels (dark) |
| Surface Light | `#FFFFFF` | Cards, panels (light) |
| Text Primary (Dark theme) | `#E2E8F0` | Dark theme text |
| Text Primary (Light theme) | `#1E293B` | Light theme text |
| Text Muted | `#94A3B8` (dark) / `#64748B` (light) | Hints, subtitles |
| Success | `#10B981` | Active/recording state (same as accent) |
| Warning | `#FBBF24` | Warnings, "trending up" badges |
| Error | `#F87171` | Errors, danger, stop-recording |

Source of truth for exact values per theme: `build/app/src/Theme.cs` (`Theme.Load`).

## Typography

| Role | Font | Size | Weight |
|------|------|------|--------|
| Title | Segoe UI | 22px | Bold |
| Section | Segoe UI | 15px | SemiBold |
| Body | Segoe UI | 13px | Regular |
| Caption | Segoe UI | 11px | Regular |
| Version | Segoe UI | 13px | Regular, Italic |

**Hebrew support:** Full RTL via Segoe UI (native Windows, includes Hebrew glyphs)

## Logo

- **Full logo:** Icon (purple rounded square with "APT") + "AutoProcess Twin" text
- **Icon only:** Purple rounded square (#6450DC) with white "APT" text, 14px bold
- **Monochrome:** Same shape in gray
- **Files:** `assets/icons/` — AppIcon.ico (16/32/48/64/128/256), AppIcon.png

## Spacing & Shape

- **Corner radius:** 8px (cards), 6px (buttons), 5px (status dot)
- **Spacing scale:** 4/8/12/16/24/32px
- **Button height:** 32px standard, 40px primary
- **Border:** 1px `rgba(255,255,255,0.08)` dark / `rgba(0,0,0,0.08)` light

## Motion

| Duration | Usage |
|----------|-------|
| 150ms | Hover transitions |
| 250ms | Button click |
| 300ms | Splash fade-in |
| 200ms | Splash fade-out |
| 400ms | Modal open |

## Voice & Tone

- **EN:** Direct, technical but friendly. "Start Recording" not "Commence Capture".
- **HE:** Natural Israeli tech-product Hebrew. "התחל הקלטה", not translated-English.
- **Error messages:** What happened + what to do. Not "Error occurred".
- **No marketing speak** in the app UI.
