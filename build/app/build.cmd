@echo off
REM בונה את AutoProcessTwin.exe (WPF, בלי .csproj - csc.exe גולמי, כמו שאר
REM המוצרים בתיקייה) ומעתיק payload מלא (agent + config + docs) ל-
REM dist\AutoProcessTwin - התיקייה הזו היא בדיוק מה שההתקנה מתקינה.
setlocal
cd /d "%~dp0"

set FX=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set CSC=%FX%\csc.exe
set WPF=%FX%\WPF
set OUT=..\dist\AutoProcessTwin

if not exist "%CSC%" (
    echo csc.exe לא נמצא ב-%FX%. ראה README לפרטים על התלות ב-.NET Framework.
    exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"

echo מקמפל AutoProcessTwin.exe...
"%CSC%" -nologo -target:winexe -win32icon:AppIcon.ico -win32manifest:app.manifest ^
  -r:"%WPF%\PresentationFramework.dll" -r:"%WPF%\PresentationCore.dll" -r:"%WPF%\WindowsBase.dll" ^
  -r:"%FX%\System.Xaml.dll" -r:"%FX%\System.Web.Extensions.dll" -r:"%FX%\System.Core.dll" ^
  -r:"%FX%\System.dll" -r:"%FX%\System.Xml.dll" -r:"%FX%\System.Security.dll" ^
  -r:"%FX%\System.Windows.Forms.dll" -r:"%FX%\System.Drawing.dll" ^
  -out:"%OUT%\AutoProcessTwin.exe" src\*.cs
if errorlevel 1 (
    echo קומפילציה נכשלה.
    exit /b 1
)

echo מעתיק AppIcon.ico...
copy /y AppIcon.ico "%OUT%\AppIcon.ico" >nul

echo מעתיק agent\ (כולל node_modules המלא - ראה README "מה למדנו" לפני שמנסים
echo   לגזום ממנו שוב: @mapbox/node-pre-gyp כן נדרש ב-runtime ע"י active-win
echo   על Windows, לא רק ל-install script כמו שהנחנו בטעות בעבר)...
robocopy ..\agent "%OUT%\agent" /e /nfl /ndl /njh /njs >nul
if errorlevel 8 (
    echo robocopy נכשל.
    exit /b 1
)

echo מעתיק config\ (רק תבניות .example.json - לא קונפיג אישי קיים)...
if not exist "%OUT%\config" mkdir "%OUT%\config"
copy /y ..\config\*.example.json "%OUT%\config\" >nul

echo מעתיק תיעוד...
copy /y ..\..\SPEC.md "%OUT%\SPEC.md" >nul
copy /y ..\..\README.md "%OUT%\README.md" >nul
if exist ..\..\FINDINGS.md copy /y ..\..\FINDINGS.md "%OUT%\FINDINGS.md" >nul

echo.
echo מוכן: %OUT%\AutoProcessTwin.exe
endlocal
