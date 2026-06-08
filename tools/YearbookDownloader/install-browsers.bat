@echo off
echo Installing Playwright browser dependencies...
echo This is required for browser automation method.
echo.

cd /d "%~dp0"

echo Building project first...
dotnet build

if %ERRORLEVEL% NEQ 0 (
    echo Build failed! Please check for errors above.
    pause
    exit /b 1
)

echo.
echo Installing browser dependencies...
pwsh bin\Debug\net10.0\playwright.ps1 install chromium

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ? Browser dependencies installed successfully!
    echo You can now run: dotnet run -- --playwright
) else (
    echo.
    echo ? Browser installation failed. Trying alternative method...
    echo Running the application once to trigger auto-installation...
    timeout /t 3 >nul
    dotnet run -- --playwright
)

echo.
pause
