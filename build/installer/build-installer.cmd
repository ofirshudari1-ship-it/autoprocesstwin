@echo off
REM בונה את AutoProcessTwin-Setup-<version>.exe: מריץ קודם את app\build.cmd (כדי ש-
REM dist\AutoProcessTwin יהיה עדכני), דוחס אותו ל-zip, ומקמפל installer גרפי
REM (WinForms wizard) שמטביע את ה-zip בתוכו כמשאב ומחלץ אותו בהתקנה.
setlocal
cd /d "%~dp0"

set FX=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set CSC=%FX%\csc.exe

if not exist "%CSC%" (
    echo csc.exe לא נמצא ב-%FX%.
    exit /b 1
)

echo === שלב 1: בונה את האפליקציה עצמה ===
call ..\app\build.cmd
if errorlevel 1 exit /b 1

echo.
echo === שלב 2: דוחס את dist\AutoProcessTwin ל-payload.zip ===
if exist payload.zip del payload.zip
powershell -NoProfile -Command "Compress-Archive -Path '..\dist\AutoProcessTwin\*' -DestinationPath 'payload.zip' -CompressionLevel Optimal"
if errorlevel 1 (
    echo כישלון בדחיסה.
    exit /b 1
)

echo.
echo === שלב 3: מקמפל את AutoProcessTwin-Setup-0.5.8.exe ===
"%CSC%" -nologo -target:winexe -win32icon:..\app\AppIcon.ico ^
  -r:System.Windows.Forms.dll -r:System.Drawing.dll ^
  -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll ^
  -resource:payload.zip,AutoProcessTwinSetup.payload.zip ^
  -out:"..\..\AutoProcessTwin-Setup-0.5.8.exe" Setup.cs
if errorlevel 1 (
    echo קומפילציית ההתקנה נכשלה.
    exit /b 1
)

del payload.zip

echo.
echo === שלב 4: מנקה קבצי installer ישנים בשורש ===
for %%F in (..\..\AutoProcessTwin-Setup-*.exe) do (
    if /I not "%%~nxF"=="AutoProcessTwin-Setup-0.5.8.exe" (
        echo מוחק ישן: %%~nxF
        del /f /q "%%F"
    )
)

echo.
echo מוכן: ..\..\AutoProcessTwin-Setup-0.5.8.exe
endlocal
