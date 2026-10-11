using System;
using System.Runtime.InteropServices;

namespace AutoProcessTwin
{
    // 0.5.10: "הרגע הבטוח" להתקנת עדכון אוטומטי. ההתקנה סוגרת את האפליקציה
    // (ועוצרת הקלטה פעילה), לכן לא מתקינים באמצע עבודה של המשתמש: רק אחרי
    // שהוא לא נגע במקלדת/עכבר MinIdleSeconds, ולעולם לא בזמן כיבוי/התנתקות
    // של Windows (התקנה שנקטעת באמצע כתיבה לתיקיית ההתקנה = התקנה שבורה).
    public static class UpdateGate
    {
        public const double MinIdleSeconds = 180;
        public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

        public static bool IsSafeToInstall(double idleSeconds, bool sessionEnding, bool installInProgress,
            DateTime utcNow, DateTime blockedUntilUtc)
        {
            if (sessionEnding || installInProgress) return false;
            if (utcNow < blockedUntilUtc) return false;
            return idleSeconds >= MinIdleSeconds;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        // שניות מאז הקלט האחרון של המשתמש. אם הקריאה נכשלת מחזיר 0 ("לא
        // idle") - ברירת מחדל בטוחה: עדיף לא להתקין מאשר להתקין בטעות.
        public static double GetIdleSeconds()
        {
            try
            {
                var info = new LASTINPUTINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                if (!GetLastInputInfo(ref info)) return 0;
                uint now = unchecked((uint)Environment.TickCount);
                uint idleMs = unchecked(now - info.dwTime);
                return idleMs / 1000.0;
            }
            catch { return 0; }
        }
    }
}
