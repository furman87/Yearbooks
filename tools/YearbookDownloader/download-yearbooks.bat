@echo off
echo ===== Yearbook Downloader =====
echo.
echo Starting browser automation download...
echo This method handles JavaScript challenges automatically.
echo.

cd /d "%~dp0"

echo Building project...
dotnet build --verbosity quiet

if %ERRORLEVEL% NEQ 0 (
    echo ? Build failed! Please check for errors.
    pause
    exit /b 1
)

echo ? Starting download with browser automation...
echo.

dotnet run -- --playwright

echo.
echo Download completed!
pause
