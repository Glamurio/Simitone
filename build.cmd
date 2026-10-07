@echo off
rem Rebuilds Simitone and deploys it to .\SimitoneWindows (see tools\build.ps1 for options).
rem Examples:  build.cmd            build.cmd -Run            build.cmd -Configuration Debug -Run
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build.ps1" %*
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo.
  echo Build failed with exit code %RC%.
  pause
)
exit /b %RC%
