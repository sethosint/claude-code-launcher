@echo off
REM Build a ready-to-distribute Claude Code Launcher
REM Requires: .NET 10 SDK (https://dotnet.microsoft.com/download)
REM Output:   publish\ClaudeLauncher\  (self-contained, no .NET install needed)

echo ============================================
echo   Claude Code Launcher — Release Build
echo ============================================
echo.

echo [1/3] Cleaning old build artifacts...
if exist Claude\bin rmdir /s /q Claude\bin
if exist Claude\obj rmdir /s /q Claude\obj
if exist publish rmdir /s /q publish
echo       Done.
echo.

echo [2/3] Building (Release, win-x64, self-contained)...
dotnet publish Claude\ClaudeLauncher.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishReadyToRun=true ^
  -o publish\ClaudeLauncher

if %errorlevel% neq 0 (
    echo.
    echo *** BUILD FAILED ***
    echo.
    pause
    exit /b 1
)

echo.
echo [3/3] Verifying output...
if not exist "publish\ClaudeLauncher\ClaudeLauncher.exe" (
    echo *** ERROR: ClaudeLauncher.exe not found in output ***
    pause
    exit /b 1
)
if not exist "publish\ClaudeLauncher\CompletionsProxy\server.js" (
    echo *** WARNING: CompletionsProxy not found — Custom API won't work ***
)

echo.
echo ============================================
echo   Build successful!
echo ============================================
echo.
echo Output: publish\ClaudeLauncher\
echo.
echo To distribute: ZIP the publish\ClaudeLauncher folder.
echo Users need Node.js 22 for OmniRoute and the API proxy.
echo.
pause
