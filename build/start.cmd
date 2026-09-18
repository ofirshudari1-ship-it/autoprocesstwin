@echo off
REM הפעלה ידנית של ה-recorder. לוחצים פעמיים, חלון נשאר פתוח, סוגרים = Ctrl+C
REM או פשוט סוגרים את החלון. בכוונה לא Scheduled Task — ראה README, פרק פרטיות.
setlocal
cd /d "%~dp0agent"
if not exist "node_modules" (
    echo מתקין תלויות בפעם הראשונה...
    call npm install
)
if not exist "..\config\privacy.json" (
    echo לא נמצא config\privacy.json - מעתיק מברירת המחדל.
    echo ערוך אותו לפני ההרצה הבאה כדי להוסיף החרגות פרטיות משלך!
    copy "..\config\privacy.example.json" "..\config\privacy.json"
)
node src\index.js
endlocal
