using System;

namespace AutoProcessTwin
{
    // מחלקת לוקליזציה פשוטה - לשנות שפה: Strings.Language = "en" ולהפעיל מחדש.
    public static class Strings
    {
        public static string Language = "en"; // "he" or "en"

        private static bool IsHe { get { return Language == "he"; } }

        // --- כותרות טאבים ---
        public static string TabHome          { get { return IsHe ? "בית" : "Home"; } }
        public static string TabPatterns      { get { return IsHe ? "פעולות חוזרות" : "Patterns"; } }
        public static string TabSettings      { get { return IsHe ? "הגדרות" : "Settings"; } }
        public static string TabHelp          { get { return IsHe ? "עזרה" : "Help"; } }

        // --- כותרות תת-טאבים ---
        public static string TabGeneral       { get { return IsHe ? "כללי" : "General"; } }
        public static string TabPrivacy       { get { return IsHe ? "פרטיות" : "Privacy"; } }
        public static string TabGuardrails    { get { return IsHe ? "Guardrails" : "Guardrails"; } }
        public static string TabAi            { get { return IsHe ? "AI (ניסיוני)" : "AI (Experimental)"; } }

        // --- הגדרות כלליות ---
        public static string LabelLanguage    { get { return IsHe ? "שפת ממשק" : "Interface Language"; } }
        public static string LabelTheme       { get { return IsHe ? "ערכת נושא" : "Theme"; } }
        public static string LabelStartup     { get { return IsHe ? "הפעלה עם Windows" : "Start with Windows"; } }
        public static string ThemeDark        { get { return IsHe ? "כהה" : "Dark"; } }
        public static string ThemeLight       { get { return IsHe ? "בהיר" : "Light"; } }
        public static string LangHe           { get { return "עברית (Hebrew)"; } }
        public static string LangEn           { get { return "English"; } }
        public static string BtnSave          { get { return IsHe ? "שמור" : "Save"; } }
        public static string BtnCancel        { get { return IsHe ? "ביטול" : "Cancel"; } }
        public static string MsgRestartNeeded { get { return IsHe ? "חלק מהשינויים ייכנסו לתוקף בהפעלה הבאה." : "Some changes will take effect after restart."; } }

        // --- כפתורים כלליים ---
        public static string BtnStartRecording  { get { return IsHe ? "▶  התחל הקלטה" : "▶  Start Recording"; } }
        public static string BtnStopRecording   { get { return IsHe ? "⏹  עצור הקלטה" : "⏹  Stop Recording"; } }
        public static string BtnRefresh         { get { return IsHe ? "רענן" : "Refresh"; } }

        // --- הודעות כלליות ---
        public static string NotRecording       { get { return IsHe ? "לא מקליט" : "Not Recording"; } }
        public static string Recording          { get { return IsHe ? "מקליט..." : "Recording..."; } }
        public static string StatusActive       { get { return IsHe ? "פעיל" : "Active"; } }
        public static string StatusStopped      { get { return IsHe ? "עצר" : "Stopped"; } }
        public static string StatusIdle         { get { return IsHe ? "לא פעיל" : "Idle"; } }
        public static string LogRecordingStarted    { get { return IsHe ? "--- מתחיל הקלטה ---" : "--- Recording started ---"; } }
        public static string LogRecorderStopped     { get { return IsHe ? "--- התהליך נעצר (exit {0}) ---" : "--- Process stopped (exit {0}) ---"; } }
        public static string CountdownNextIn        { get { return IsHe ? "התצפית הבאה בעוד {0}s" : "Next observation in {0}s"; } }
        public static string CountdownCapturingNow  { get { return IsHe ? "מצלם עכשיו..." : "Capturing now..."; } }

        // --- הגדרות כללי - תיאורים ---
        public static string GeneralSettingsTitle   { get { return IsHe ? "הגדרות כלליות" : "General Settings"; } }
        public static string StartupDesc            { get { return IsHe ? "הפעל את AutoProcess Twin אוטומטית בכניסה ל-Windows" : "Launch AutoProcess Twin automatically at Windows startup"; } }

        // --- מקטע הפעלה ---
        public static string SectionBehavior        { get { return IsHe ? "התנהגות" : "Behavior"; } }
        public static string LabelAutoRecord        { get { return IsHe ? "התחל הקלטה אוטומטית בהפעלה" : "Auto-start recording on launch"; } }
        public static string AutoRecordDesc         { get { return IsHe ? "מתחיל להקליט מיד עם פתיחת האפליקציה, בלי ללחוץ 'התחל'" : "Starts recording immediately when the app opens, without clicking Start"; } }
        public static string LabelCheckUpdates      { get { return IsHe ? "בדוק עדכונים אוטומטית" : "Automatically check for updates"; } }
        public static string CheckUpdatesDesc       { get { return IsHe ? "בדיקה שקטה מול GitHub בכל הפעלה; לא מוריד ולא מתקין כלום לבד - רק מודיע" : "A quiet check against GitHub on each launch; never downloads or installs anything on its own - only notifies"; } }
        public static string BtnCheckForUpdates     { get { return IsHe ? "בדוק עדכונים עכשיו" : "Check for Updates Now"; } }
        public static string LabelStartMinimized    { get { return IsHe ? "התחל ממוזער למגש" : "Start minimized to tray"; } }
        public static string StartMinimizedDesc     { get { return IsHe ? "האפליקציה תיפתח ישר למגש המערכת, בלי להציג חלון (לא חל על ריצה ראשונה)" : "The app opens straight to the system tray without showing a window (does not apply to the very first run)"; } }
        public static string MsgStartupRegFailed    { get { return IsHe ? "לא הצלחנו לעדכן את הגדרת ההפעלה עם Windows ברישום (Registry). נסו שוב, או בדקו הרשאות/תוכנת אנטי-וירוס." : "Couldn't update the Windows startup registry entry. Try again, or check permissions/antivirus."; } }

        // --- Global shortcut (§12.4) ---
        public static string SectionHotkey          { get { return IsHe ? "קיצור מקלדת גלובלי" : "Global Shortcut"; } }
        public static string LabelHotkeyEnable      { get { return IsHe ? "אפשר קיצור מקלדת גלובלי להתחלה/עצירה של הקלטה" : "Enable a global shortcut to start/stop recording"; } }
        public static string HotkeyEnableDesc       { get { return IsHe ? "עובד מכל מקום ב-Windows, גם כשהאפליקציה לא בפוקוס - שימושי להשהיית הקלטה מהר לצורכי פרטיות" : "Works from anywhere in Windows, even when the app isn't focused - handy for pausing recording quickly for privacy"; } }
        public static string LabelHotkeyModifier    { get { return IsHe ? "מקשי עזר" : "Modifier keys"; } }
        public static string LabelHotkeyKey         { get { return IsHe ? "מקש" : "Key"; } }
        public static string MsgHotkeyConflict      { get { return IsHe ? "לא הצלחנו לרשום את הקיצור - כנראה שהוא כבר תפוס על ידי תוכנה אחרת. נסו שילוב אחר." : "Couldn't register the shortcut - it's likely already in use by another application. Try a different combination."; } }
        public static string MsgHotkeyLikelyTaken   { get { return IsHe ? "השילוב שבחרתם נפוץ אצל תוכנות אחרות (למשל דפדפן/Discord/OBS). אם הוא לא עובד כמצופה, נסו שילוב אחר." : "The combination you picked is commonly used by other apps (e.g. browser/Discord/OBS). If it doesn't behave as expected, try a different one."; } }

        // --- מקטע נתונים ---
        public static string SectionData            { get { return IsHe ? "קבצים ותיקיות" : "Files & Folders"; } }
        public static string BtnOpenReports         { get { return IsHe ? "פתח תיקיית דוחות" : "Open Reports Folder"; } }
        public static string BtnOpenData            { get { return IsHe ? "פתח תיקיית נתונים" : "Open Data Folder"; } }

        // --- מקטע אבטחה ---
        public static string SectionSecurity        { get { return IsHe ? "אבטחה ופרטיות" : "Security & Privacy"; } }
        public static string SecurityApiKeyInfo     { get { return IsHe ? "מפתח ה-API מוצפן עם DPAPI (הצפנת Windows לפי משתמש)." : "API key encrypted with Windows DPAPI (per-user encryption)."; } }
        public static string SecurityNoKeylog       { get { return IsHe ? "אין רישום הקשות — רק שם חלון פעיל נקלט." : "No keystroke logging — only active window title is captured."; } }
        public static string SecurityNoAudio        { get { return IsHe ? "אין הקלטת שמע — רשמקול מושבת לחלוטין." : "No audio recording — microphone fully disabled."; } }
        public static string SecurityLocalOnly      { get { return IsHe ? "כל הנתונים מאוחסנים מקומית בלבד." : "All data stored locally only."; } }
        public static string SecurityFilterFirst    { get { return IsHe ? "מסנן פרטיות פועל לפני כל שמירה." : "Privacy filter runs before any data is saved."; } }

        // --- גרסה ---
        public static string LabelVersion           { get { return IsHe ? "גרסה" : "Version"; } }
        public static string LabelDataDir           { get { return IsHe ? "תיקיית נתונים" : "Data folder"; } }

        // --- Tray ---
        public static string TrayTooltip            { get { return IsHe ? "AutoProcess Twin — פעיל ברקע" : "AutoProcess Twin — running in background"; } }
        public static string TrayTooltipRecording   { get { return IsHe ? "AutoProcess Twin — מקליט..." : "AutoProcess Twin — Recording..."; } }
        public static string TrayBalloonTitle       { get { return IsHe ? "AutoProcess Twin פועל ברקע" : "AutoProcess Twin is running in the background"; } }
        public static string TrayBalloonText        { get { return IsHe ? "ההקלטה ממשיכה. לחצו על אייקון המגש להחזיר את החלון." : "Recording continues. Click the tray icon to reopen."; } }
        public static string TrayMenuOpen           { get { return IsHe ? "פתח" : "Open"; } }
        public static string TrayMenuExit           { get { return IsHe ? "יציאה" : "Exit"; } }
        public static string TrayMenuStartRec       { get { return IsHe ? "התחל הקלטה" : "Start Recording"; } }
        public static string TrayMenuStopRec        { get { return IsHe ? "עצור הקלטה" : "Stop Recording"; } }

        // --- About / Footer ---
        public static string AppCopyright           { get { return IsHe ? "© 2026 AutoProcess Twin. כל הזכויות שמורות." : "© 2026 AutoProcess Twin. All rights reserved."; } }
        public static string AppDescription         { get { return IsHe ? "Phase 1: הקלטת פעילות, יומן פעילות יומי, וזיהוי פעולות חוזרות עם המלצות אוטומציה. אין Autopilot, אין רישום הקשות, אין הקלטת שמע." : "Phase 1: Activity recording, daily briefing, and repetitive-action detection with automation suggestions. No autopilot, no keylogging, no audio capture."; } }
        public static string FooterText             { get { return IsHe ? "הכל מקומי — שום דבר לא נשלח לענן. × = מזעור למגש; יציאה דרך התפריט." : "Everything local — nothing is sent to the cloud. × = minimize to tray; exit via the menu."; } }
        public static string HeaderSubtitle         { get { return IsHe ? "סייען אוטומציה + כפיל דיגיטלי — Phase 1" : "Automation assistant + digital twin — Phase 1"; } }

        // --- Splash ---
        public static string SplashLoading          { get { return IsHe ? "טוען..." : "Loading..."; } }
        public static string SplashReady            { get { return IsHe ? "מוכן" : "Ready"; } }

        // --- Onboarding ---
        public static string OnboardingTitle        { get { return IsHe ? "ברוך הבא ל-AutoProcess Twin" : "Welcome to AutoProcess Twin"; } }
        public static string BtnSkip                { get { return IsHe ? "דלג" : "Skip"; } }
        public static string BtnBack                { get { return IsHe ? "הקודם" : "Back"; } }
        public static string BtnNext                { get { return IsHe ? "הבא" : "Next"; } }
        public static string BtnFinish              { get { return IsHe ? "סיים" : "Finish"; } }

        // --- Buttons existing ---
        public static string BtnShowOnboarding      { get { return IsHe ? "📖  הצג שוב את מדריך הפתיחה" : "📖  Show Welcome Guide Again"; } }
        public static string BtnOpenSpec            { get { return IsHe ? "פתח SPEC.md (אפיון)" : "Open SPEC.md (Specification)"; } }
        public static string BtnOpenReadme          { get { return IsHe ? "פתח README.md" : "Open README.md"; } }
        public static string BtnOpenFindings        { get { return IsHe ? "פתח FINDINGS.md" : "Open FINDINGS.md"; } }
        public static string BtnOpenDataFolder      { get { return IsHe ? "פתח תיקיית דאטה" : "Open Data Folder"; } }
        public static string BtnOpenConfigFolder    { get { return IsHe ? "פתח תיקיית קונפיג" : "Open Config Folder"; } }
        public static string BriefingTitle          { get { return IsHe ? "יומן פעילות מפורט" : "Detailed Activity Log"; } }
        public static string BriefingDesc           { get { return IsHe ? "הדוח היומי הגולמי — מסקנות מהיומן." : "The raw daily report — conclusions from the log."; } }
    }
}
