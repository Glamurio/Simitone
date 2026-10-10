@echo off
rem Unattended crash test: plays every lot of the neighbourhood at top speed with random Sim actions and writes a report.
rem   run-smoke.cmd [folder with Simitone.Windows.exe] [extra Simitone switches]
rem Examples:
rem   run-smoke.cmd                                   (uses SimitoneWindows in the repository, where build.cmd puts it)
rem   run-smoke.cmd -smokeall                         (switches may come first; the default folder is used)
rem   run-smoke.cmd C:\Games\SimitoneDev -smokeminutes=60 -smokelots=21,22
rem   run-smoke.cmd "" -path "B:\Steam\steamapps\common\The Sims Legacy Collection"
rem It uses its own folder, %USERPROFILE%\Documents\Simitone\smoke, so your own neighbourhoods are never touched.
setlocal
rem The first argument is the game folder, unless it is missing, empty ("") or already a switch such as -smokeall.
set "FIRST=%~1"
set "EXEDIR="
if not defined FIRST (
  shift
) else if "%FIRST:~0,1%"=="-" (
  rem a switch: keep it for the game
) else (
  set "EXEDIR=%FIRST%"
  shift
)
if not defined EXEDIR set "EXEDIR=%~dp0..\..\SimitoneWindows"
if not exist "%EXEDIR%\Simitone.Windows.exe" if exist "%~dp0..\..\artifacts\SimitoneWindows\Simitone.Windows.exe" set "EXEDIR=%~dp0..\..\artifacts\SimitoneWindows"
if not exist "%EXEDIR%\Simitone.Windows.exe" (
  echo Simitone.Windows.exe not found in "%EXEDIR%". Build first with tools\build.ps1 or pass the folder as the first argument.
  exit /b 3
)
set "ARGS="
:collect
if "%~1"=="" goto run
set ARGS=%ARGS% %1
shift
goto collect
:run
echo Running smoke test with %EXEDIR%\Simitone.Windows.exe -smoke%ARGS%
"%EXEDIR%\Simitone.Windows.exe" -smoke%ARGS%
set CODE=%ERRORLEVEL%
echo Crash details, if any: %USERPROFILE%\Documents\Simitone\crash-log.txt
echo Exit code %CODE%  (0 = pass, 1 = problems found, 2 = crashed)
for /f "delims=" %%F in ('dir /b /o-d "%USERPROFILE%\Documents\Simitone\smoke\reports\smoke-*.txt" 2^>nul') do (
  echo Report: %USERPROFILE%\Documents\Simitone\smoke\reports\%%F
  goto done
)
:done
exit /b %CODE%
