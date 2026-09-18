@echo off
REM מייצר את דוח התדרוך היומי מהדאטה שכבר הוקלטה, ופותח אותו.
setlocal
cd /d "%~dp0agent"
node src\briefing.js
if exist "..\..\reports" (
    for /f "delims=" %%f in ('dir /b /o-d "..\..\reports\morning-briefing-*.md" 2^>nul') do (
        start "" "..\..\reports\%%f"
        goto :done
    )
)
:done
endlocal
