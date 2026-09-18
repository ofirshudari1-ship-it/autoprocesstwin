# AutoProcess Twin

מנוע זיהוי פעולות חוזרות + המלצות אוטומציה, מעל recorder + יומן פעילות.
ראה `SPEC.md` לעדכון המטרה האמיתית (10.9.2026). רץ מקומית על Windows, שום
דבר לא יוצא מהמחשב הזה.

> **מה זה בפועל, בקצרה:** תוכנה שרצה ברקע, שולפת כל 20 שניות איזה חלון פעיל
> אצלך, מצלמת מסך (חוץ מיישומים שהחרגת), ושומרת הכול ב-SQLite מקומי. אחרי
> כמה ימים, טאב "המלצות אוטומציה" מזהה מה חוזר על עצמו (תיעוד CRM, העברת
> קבצים...) ומציע מה יכול לחסוך את הזמן הזה. שום שליחת מייל, שום נגיעה
> אוטומטית ב-CRM, שום פעולה אוטונומית — זה עדיין לא קיים, ולמה זה נשאר ככה
> בכוונה מוסבר למטה.

---

## התקנה גרפית (GUI + Installer)

יש עכשיו אפליקציית שולחן-עבודה אמיתית (WPF, ערכת נושא כהה/אמרלד תואמת לשאר
המוצרים בתיקייה) וקובץ התקנה גרפי מעוצב:

```
Setup-AutoProcessTwin.exe
```

לחיצה כפולה פותחת אשף התקנה (מבקש הרשאות מנהל/UAC - מתקין ל-
`C:\Program Files\AutoProcessTwin`, יוצר קיצורי דרך בשולחן העבודה
ובתפריט התחל, ונרשם ב"הגדרות > אפליקציות" עם הסרה רגילה). האפליקציה עצמה
(`AutoProcessTwin.exe`) היא לוח בקרה מלא: כפתור התחל/עצור הקלטה, יומן
פעילות חי, עריכת רשימות ההחרגה של הפרטיות, עריכת ה-Guardrails, וצפייה
בדוחות תדרוך - בלי לערוך JSON ידנית (אפשר עדיין, אם רוצים).

**מקור:** `app/` (קוד ה-GUI, C#/WPF) ו-`installer/` (Setup.cs). לבנייה מחדש:
`installer\build-installer.cmd`. פירוט מלא על טכניקת ה-build (בלי .csproj -
csc.exe גולמי, כמו שאר המוצרים בריפו) ועל התקלות שעלו תוך כדי, ב-`build/FINDINGS.md`.

### Program Files, לא AppData (0.5.1 - החלטת בעלים מפורשת, דורסת את הקודמת)

עד 0.5.0 ההתקנה נכתבה בכוונה ל-`%LocalAppData%\Programs\AutoProcessTwin` -
הסעיף הזה הסביר אז למה: אין תועלת פונקציונלית ל-elevation כי ה-recorder צריך
לצפות בסשן הדסקטופ האינטראקטיבי של המשתמש המחובר, לא הרשאות מערכת.

ב-2026-09-17 אופיר שודרי נתן הנחיה מפורשת ישירה שדורסת את זה: **כל הכלים
בפורטפוליו יותקנו כברירת מחדל תחת `C:\Program Files`**, עקביות בין המוצרים
גוברת על החיסכון ב-UAC prompt אחד בהתקנה. ההשלכה בפועל:

- **המתקין (Setup.cs)** דורש הרשאות מנהל ומרים UAC אוטומטית אם לא רץ מורם
  (`Program.Main` - runas relaunch, כמו במתקין של OptiGuard), כותב ל-
  `C:\Program Files\AutoProcessTwin`, ונרשם ב-`HKLM\...\Uninstall` (לא HKCU).
- **האפליקציה עצמה נשארת `asInvoker`** (`app.manifest`) - היא **לא** רצה
  מורמת אחרי ההתקנה. הנימוק המקורי (הצורך בסשן אינטראקטיבי, לא ב-elevation)
  עדיין נכון ולא בוטל - רק מיקום ההתקנה עצמו הוא שהשתנה, לפי הנחיה מפורשת של
  הבעלים שגוברת על השיקול הזה.
- **דאטה הניתנת לכתיבה זזה החוצה מ-Program Files**, שהוא read-only למשתמש
  לא-מורם אחרי ההתקנה: `config/*.json` (privacy/guardrails/ai/app),
  `data/twin.db`+screenshots, ו-`reports/` כולם עברו ל-
  `%LocalAppData%\AutoProcessTwin` - גם בצד ה-C# (`AppPaths.cs`) וגם בצד
  ה-Node (`agent/src/config.js`, שקורא `process.env.LOCALAPPDATA` ישירות, בלי
  תלות ב-C#). `Program Files\AutoProcessTwin` מכיל אחרי ההתקנה רק payload
  קבוע לקריאה בלבד: ה-exe, `agent/` (כולל `node_modules`), ו-`config/*.example.json`.
- **מיגרציה חד-פעמית** להתקנות 0.5.0 ומטה: המתקין (אם מזהה רשומת HKCU ישנה)
  מעתיק `config/data/reports` הישנים אל `%LocalAppData%\AutoProcessTwin`
  ומנקה את ה-HKCU key/ה-Run entry/תיקיית ה-AppData הישנה; `AppPaths.
  MigrateLegacyDataIfNeeded()` באפליקציה עצמה הוא רשת ביטחון נוספת (אידמפוטנטי)
  למקרה שהמתקין לא רץ (למשל exe הועתק ידנית).
- **המחיר של הבחירה:** UAC prompt אחד בהתקנה/עדכון/הסרה, ושההתקנה כעת
  per-machine (לא per-user) - למי שרוצה מספר משתמשי Windows על אותו מחשב, כל
  אחד עדיין מגדיר ומקליט בנפרד (הדאטה עצמה תמיד per-user, ב-`%LocalAppData%`).

**עדכון אחרי באג אמיתי שנתפס:** גרסה קודמת התקינה ורצה בלי לקרוס, אבל
ההקלטה בפועל לא עבדה בכלל (node_modules נגזם בטעות, שבר את `active-win`) -
ראה `FINDINGS.md` סעיף "תיקון קריטי". **עכשיו יש בדיקה אמיתית מקצה לקצה:**
`AutoProcessTwin.exe --selftest` מריץ את בדיוק לוגיקת כפתור "התחל הקלטה"
ומוודא שנכתב אירוע אמיתי ל-DB - לא רק שהתהליך לא קרס. עבר בהצלחה אחרי התיקון.

**מגבלה שנשארת:** לא הצלחתי לוודא ויזואלית (screenshot) שה-UI *נראה* תקין -
הרשאת שיתוף מסך נדחתה. `--selftest` מוכיח שההקלטה עובדת מקצה לקצה, לא שהעימוד
נראה טוב. מומלץ שתריץ ותסתכל בעצמך.

---

## התחלה מהירה

```bash
cd agent
npm install
cp ../config/privacy.example.json ../config/privacy.json
cp ../config/guardrails.example.json ../config/guardrails.json
```

ערוך את `config/privacy.json` — הוסף כל אפליקציה/מילת מפתח רגישה (בנק, מנהל
סיסמאות, לקוח ספציפי) שאסור לצלם. זה חייב לקרות **לפני** הרצה ראשונה.

```bash
npm start          # מתחיל להקליט — Ctrl+C לעצירה
npm run briefing   # מייצר דוח על מה שהוקלט היום
```

או פשוט הרצה כפולה על `start.cmd` / `briefing.cmd` בשורש הפרויקט (מתקינים
תלויות אוטומטית בפעם הראשונה).

הדוח נשמר ב-`reports/morning-briefing-<תאריך>.md`.

**אין הפעלה אוטומטית ב-startup / Scheduled Task.** בכוונה — הקלטת מסך היא פעולה
עם השלכות פרטיות אמיתיות (גם שלך וגם של כל מי שמופיע במסך שלך — לקוחות,
עמיתים). אתה מפעיל אותה ביודעין כל פעם, לא רץ שקט ברקע בלי שתדע.

---

## מה בנוי ועובד עכשיו (Phase 1)

| רכיב | קובץ | סטטוס |
|---|---|---|
| קריאת חלון פעיל (app + title) | `agent/src/index.js` (via `active-win`) | ✅ עובד, נבדק |
| צילום מסך | `agent/src/screenshot.js` + `screenshot.ps1` | ✅ עובד, נבדק |
| Privacy Filter (החרגת אפליקציות/מילות מפתח) | `agent/src/privacyFilter.js` | ✅ עובד, נבדק (`agent/src/selftest.js`) |
| רדקציה בסיסית (מספרי אשראי/ת"ז ב-OCR) | `agent/src/privacyFilter.js` | ✅ נבדק, best-effort ולא מושלם |
| אחסון מקומי (SQLite, `node:sqlite`) | `agent/src/db.js` | ✅ עובד, נבדק |
| Guardrail config (סכימה מהאפיון) | `config/guardrails.example.json` | ✅ סכימה מוכנה + עורך גרפי, **לא אוכפת שום דבר בפועל עדיין** |
| Morning Briefing (מבוסס על מה שהוקלט) | `agent/src/briefing.js` | ✅ עובד, נבדק על דאטה אמיתי |
| ניקוי retention אוטומטי | `agent/src/db.js: purgeOlderThan()` | ✅ עובד, נבדק עם שורה מזויפת בת 40 יום |
| OCR | `agent/src/ocr.js` | ✅ tesseract מותקן ועובד - נבדק על צילום אמיתי (3518 תווים) |
| GUI + Installer | `app/`, `installer/` | ✅ מותקן על המחשב הזה, נבדק מקצה לקצה (`--selftest`, 20/20 בדיקות), כולל עדכון והסרה |
| שקיפות Privacy Filter (מה סונן ולמה) | `agent/src/briefing.js`, `filter_reason` ב-DB | ✅ נבדק עם תרחיש אמיתי (recorder + אפליקציה מוחרגת בפועל) |
| אשף פתיחה (Onboarding) | `app/src/OnboardingWizard.cs` | ✅ נבדק **ויזואלית** (screenshot אמיתי) - כל 4 העמודים, כולל "התחל הקלטה מיד" |
| RTL + עיצוב תקין | `app/src/Theme.cs`, `MainWindow.cs` | ✅ נבדק ויזואלית - היה שבור (LTR מלא, ComboBox ריק), תוקן ואומת |
| **זיהוי פעולות חוזרות + המלצות אוטומציה** | `agent/src/patterns.js`, טאב "פעולות חוזרות" | ✅ **זה המוצר בפועל** - נבדק ויזואלית + כ-40 בדיקות ב-`selftest.js` (מספר מדויק ב-CHANGELOG.md, משתנה קצת בין הרצות בגלל תזמון DB): קיבוץ 5 שמות לקוח לדפוס אחד, קטגוריזציה (8 קטגוריות - CRM/חשבוניות/תמיכה/קבצים/גיליונות/מייל/הודעות/תמלול), סף מניעת false positive, ציון ביטחון, פילוח קטגוריה |
| דיוק חישוב זמן-לאירוע | `agent/src/patterns.js: computeEventMinutes` | ✅ **תוקן באג אמיתי** - היה ממוצע גלובלי אחד לכל החלון (פערי הקלטה מנפחים הכל), עבר לפער אמיתי לכל אירוע; נבדק ב-regression ייעודי שהיה נכשל תחת הקוד הישן |
| טווח ניתוח לבחירה (7/14/30/90 יום) | ComboBox בטאב "פעולות חוזרות" | ✅ נבדק ב-CLI ישירות (`periodDays` בפלט תואם את הבחירה) |
| מגמה (↑/↓/יציב/חדש) | `agent/src/patterns.js: classifyTrend` | ✅ נבדק (selftest + ויזואלית) - השוואה אמיתית בין מחצית ראשונה/שנייה של התקופה, לא הערכה |
| ייצוא דוח HTML לשיתוף | `agent/src/patterns-report.js: toHtml`, כפתור בטאב | ✅ נבדק ויזואלית בדפדפן - נראה כמו חלק מאותו מוצר (טוקני Theme.cs) |
| דיסמיס/התעלמות מהמלצה + ביטול | `agent/src/ignoredPatterns.js`, סקשן "דפוסים שהוסתרו" בטאב "פעולות חוזרות" | ✅ **מחזור מלא, לא רק דיסמיס חד-כיווני** - "השב תצוגה" מחזיר דפוס שהוסתר; אומת מקצה לקצה בממשק האמיתי מול DB מותקן אמיתי (לא רק selftest) |
| ארכיטקטורת UI: 4 טאבים, auto-refresh, Spotlight | `app/src/MainWindow.cs` | ✅ נבדק ויזואלית - עיצוב מחדש מלא, בלי כפתורי "עדכן X" ידניים, מתעדכן לבד ברקע (30s/3min) |
| רענון ברקע בלי הקפאת UI | `RefreshStatus`/`RefreshPatterns`/`DismissPattern`/`UnignorePattern`/`GenerateBriefingAndShow` | ✅ **תוקן** - כל אלה רצו סינכרונית על ה-UI thread (עד 30s חסימה); עברו ל-`async`/`Task.Run` + דגלי re-entrancy |
| סיכום AI אופציונלי (כבוי כברירת מחדל) | `agent/src/ai-insights.js`, כרטיס "סיכום מנהלים (AI)" בטאב "פעולות חוזרות" | ✅ **חוליה שלמה עד הסוף** (לא רק הגדרות) - כפתור בטאב הפעולות החוזרות קורא בפועל ל-ai-insights.js ברקע (Task.Run, לא חוסם UI), מציג תוצאה/שגיאה/מצב כבוי; נבדק ויזואלית + 2 בדיקות selftest חדשות (27/27) |
| מניעת הרצה כפולה (single-instance) | `app/src/Program.cs` | ✅ **תוקן בפועל** - הבאג היה Mutex שנשמר במשתנה מקומי ונאסף ע"י ה-GC (לא ה-`Global\` prefix כמו שהונח קודם), עבר ל-שדה static; נבדק עם 4 הפעלות רצופות דרך שני מסלולים שונים - נשאר process אחד |

**ראה [`build/FINDINGS.md`](build/FINDINGS.md) לפירוט מלא, צעד-אחר-צעד, של איך
כל זה עובד, כולל באג קריטי אמיתי (הקלטה לא עבדה בכלל) שנתפס ותוקן.**

---

## מה לא בנוי, ולמה בכוונה

### 1. Autopilot אמיתי (סגירת עסקאות, שליחת מיילים בלילה)

**האפיון המקורי מבקש שהמערכת תשלח הודעות ותסגור עסקאות לגמרי לבד.** זה לא נבנה,
ולא ייבנה בצורה שרצה בלי אישור אדם — לא כי אי אפשר טכנית (Gmail/CRM APIs קיימים),
אלא כי:

- אני (Claude) פועל תחת כלל שמחייב אישור מפורש שלך, בצ'אט, לפני שליחת כל הודעה
  בשמך — לא משנה כמה "בטוח" הכלל שהגדרת. זו לא מגבלה שאני יכול לעקוף.
- גם ברמה העסקית: agent שסוגר עסקאות אמיתיות בלי בדיקה סופית הוא נקודת כשל
  יקרה מדי (טעות תמחור אחת, לקוח שמרגיש שדיברו איתו רובוט) לעומת מה שהוא חוסך.

**הדרך הנכונה קדימה, כשתרצה:** שכבת "Live Assist" — ה-agent מכין טיוטת תשובה
לפי `decision_rules` וה-tone שנלמד, אתה קורא ולוחץ שלח. זה כבר 80% מהחיסכון
בזמן בלי הסיכון.

### 2. הקלטת הקשות מקלדת (Keystroke content)

האפיון מבקש "Keystroke, Mouse & UI Event Stream". **לא נבנה בכוונה.** זה בעצם
keylogger אמיתי — יתפוס גם סיסמאות שהוקלדו באפליקציות שלא הוחרגו, ידגל
כתוכנה זדונית ע"י אנטי-וירוס, ולא נחוץ למטרה בפועל: שם החלון + OCR על המסך
כבר נותנים "מה עבדת עליו ומתי" בלי לתעד כל תו שהקלדת. אם בעתיד יתברר שבאמת
צריך את זה (למשל למדידת מהירות הקלדה, לא תוכן), אפשר לבנות ספירת הקשות בלי
תוכן — לא את זה.

### 3. הקלטת שמע (Audio/Voice Capture)

לא נבנה. מוסיף עוד ממד פרטיות (שיחות טלפון, פגישות זום עם צד ג') בלי ערך מיידי
ברור מעל מה שכבר יש. אם תרצה בהמשך — שיחות Zoom/Teams כבר מוקלטות ומתומללות
ע"י כלים ייעודיים (Gong, Granola) שיש להם גם MCP connectors; עדיף לחבר לאלה
מאשר לבנות הקלטת שמע גולמית מאפס.

### 4. "צל דיגיטלי" שלומד טון וסגנון (Cognitive Logic & Pattern Engine)

זה השלב הכי "AI" באפיון, והכי לא בנוי. יש לזה תלות אמיתית: קודם צריך שבועות
של דאטה אמיתי מה-recorder (Phase 1) לפני שיש על מה ללמד מודל. סדר הבנייה
הנכון הוא: תפעיל את ה-recorder כמה שבועות → נבדוק אם ה-OCR/window-titles
מספיקים כדי לשחזר "מה החלטת ולמה" → רק אז שווה לבנות שכבת ניתוח מעליהם.

### 5. Guardrail Dashboard כממשק גרפי

יש סכימה (`config/guardrails.example.json`) שממפה בדיוק את מה שהאפיון מתאר
(pricing_boundaries, decision_rules, escalation_triggers), אבל עריכה היא כרגע
עריכת JSON ולא מסך. אם ה-Phase 1 יוכיח את עצמו, מסך עריכה (אפילו סתם דף HTML
מקומי) הוא תוספת קטנה — לא בנוי כרגע כי אין עדיין guardrails אמיתיים שמשהו
אוכף.

---

## מה חסר כדי שזה יעבוד במלואו (התקנות ידניות)

### OCR — tesseract
**כבר מותקן על המחשב הזה** (`winget install UB-Mannheim.TesseractOCR`), ונבדק
שעובד. על מחשב אחר שבו זה לא מותקן: screenshots עדיין נשמרים אבל בלי טקסט
מחולץ - הקלטת פעילות עדיין עובדת, OCR רק מוסיף הקשר. להתקין:

```
winget install UB-Mannheim.TesseractOCR
# או הורדה ידנית: https://github.com/UB-Mannheim/tesseract/wiki
```

`ocr.js` מוצא את tesseract גם בלי PATH מעודכן (נתיב ההתקנה הידוע כ-fallback) -
אין צורך להפעיל מחדש את המחשב אחרי ההתקנה.

### npm audit warnings
`npm audit` מדווח על 6 חולשות (5 high, 1 critical) בשרשרת ההתקנה של
`active-win` (`node-pre-gyp` → `node-gyp` → `tar` ישן). **אלה משפיעות רק על
סקריפט ההתקנה החלופי (`node-gyp rebuild`) שלא רץ בפועל** — `active-win`
עובד עם בינארי מוכן מראש בלי לגעת בקוד הזה (נבדק). לא תוקן כי אין גרסה
חדשה יותר של `active-win` עם שרשרת תלויות נקייה נכון לעכשיו; מומלץ להריץ
`npm audit` שוב מדי כמה חודשים ולעדכן אם משהו זז.

---

## מבנה

```
SPEC.md                         האפיון המקורי, כפי שהתקבל
CHANGELOG.md                    יומן גרסאות (Keep a Changelog)
site/index.html                 דף נחיתה דו-לשוני (אנגלית ברירת מחדל, RTL בעברית)
AutoProcessTwin-Setup-<ver>.exe קובץ ההתקנה הגרפי בשורש (נבנה מ-build/installer/)
build/FINDINGS.md               סיכום ממצאים מפורט — איך זה עובד בדיוק, מה נבדק, ממצא ה-AV
build/app/                      קוד ה-GUI (C#/WPF, ראה למטה) + start.cmd/briefing.cmd
  src/*.cs                      Theme, MainWindow, ProcessManager (מריץ/עוצר את ה-recorder), ConfigStore
  build.cmd                     בונה AutoProcessTwin.exe -> dist/AutoProcessTwin/
  AppIcon.ico                   אייקון "עין" - נוצר ע"י tools/GenIcon.cs
installer/                      קוד ההתקנה (WinForms wizard, Setup.cs)
  build-installer.cmd           מריץ app/build.cmd ואז בונה את Setup-AutoProcessTwin.exe
dist/AutoProcessTwin/           הפלט הארוז - בדיוק מה שההתקנה מתקינה (נוצר ע"י build.cmd, לא ב-git)
config/
  privacy.example.json          תבנית — החרגות פרטיות + מרווח הקלטה
  guardrails.example.json       תבנית — סכימת ה-Guardrail Dashboard מהאפיון
  ai.example.json               תבנית — AI אופציונלי, כבוי כברירת מחדל (endpoint/model/apiKey)
agent/
  package.json
  src/
    index.js                    הלולאה הראשית — recorder
    config.js                   טעינת config עם נפילה ל-.example.json
    privacyFilter.js             isExcluded() + redact()
    db.js                       node:sqlite — schema + insert/query
    screenshot.js + .ps1        צילום מסך (System.Drawing, לא תלוי בחבילת npm שבורה)
    ocr.js                      עטיפת tesseract CLI, best-effort
    briefing.js                 מייצר את דוח התדרוך
    patterns.js                 זיהוי פעולות חוזרות + קטגוריזציה + מגמה — הליבה של המוצר
    patterns-report.js          CLI: מריץ analyzePatterns, כותב .md + .html
    ignoredPatterns.js          דפוסים שסומנו "כבר טיפלתי בזה"
    ai-insights.js              סיכום AI אופציונלי — נתונים מצטברים בלבד, נופל בחן אם כבוי/אין מפתח
data/                           (נוצר אוטומטית, ב-.gitignore) twin.db + screenshots/
reports/                        דוחות תדרוך בוקר (ב-.gitignore)
```

---

## הערות טכניות שיחסכו זמן

- **אל תשתמש בחבילת `screenshot-desktop`.** נוסתה ונכשלה על המכונה הזו — היא
  מסתמכת על טריק ישן של batch-file שמקמפל את עצמו דרך `csc.exe` (`.NET
  Framework`), וזה נופל בשקט. הוחלפה ב-`screenshot.ps1` עצמאי
  (`System.Windows.Forms` + `System.Drawing`, ~10 שורות, קל לדבג).
- **שמות תיקיות בעברית שוברים כלים ישנים של Windows.** נתקלנו בזה כשהתיקייה
  נקראה בעברית — `cmd.exe`/`csc.exe` נכשלים כשה-cwd מכיל תווי עברית (בעיית
  code page). לכן שם התיקייה `AutoProcessTwin` הוא ASCII בלבד, כמו כל שאר
  התיקיות בריפו הזה (PureSys, Relay, Tablify וכו').
- **`node:sqlite` (built-in ב-Node 22+/24) נבחר על פני `better-sqlite3`** כדי
  להימנע מקומפילציית native addon (`node-gyp`) — פחות דברים שיכולים להישבר
  בהתקנה על מכונה אחרת.
- **`active-win@8.2.1` עובד בלי להריץ את סקריפט ההתקנה שלו** (npm חוסם
  postinstall scripts כברירת מחדל כיום) — יש לו בינארי Windows מוכן מראש.
  אושר ידנית שזה עובד; אל תאשר את ה-install script בלי סיבה טובה.
- **אל תוסיף codec/quality tuning ל-`screenshot.ps1`.** נוסה (JPEG עם
  `EncoderParameters`/`Quality`) ו-Windows Defender חסם את זה מיד כ-
  "ScriptContainedMaliciousContent" (AMSI, ThreatID 2147743659) — פעמיים ברצף,
  בעוד שגרסת ה-PNG הפשוטה עבדה נקי בכל הרצה. פירוט מלא ב-`FINDINGS.md` §1.
  אם צריך לצמצם גודל קובץ, השתמש ב-retention (`purgeOlderThan`), לא בכיווץ.
- **PNG מלא-מסך שוקל ~450-550KB.** בקצב ברירת המחדל (פעם בדקה) זה ~650-800MB
  ליום אם ה-recorder רץ 24/7 — `retention_days` ב-`privacy.json` (ברירת מחדל
  30) הוא מה ששומר את זה בגבול, לא שיטת הקידוד.
- **עצירה (`Ctrl+C`) לא אומתה במלואה בסביבת הפיתוח** — ראה `FINDINGS.md` §4.
  אמורה לעבוד סטנדרטית בטרמינל אינטראקטיבי אמיתי; אם ה-DB לא נסגר נקי בפועל
  אצלך, תדווח.
