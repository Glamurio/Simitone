<#
.SYNOPSIS
    Builds Simitone (Simitone.Windows + FreeSO engine) and deploys the result to .\SimitoneWindows.

.DESCRIPTION
    One-step local rebuild for development:
      1. Initialises the FreeSO git submodule if it has never been checked out
         (an existing checkout is never touched, so local FreeSO work is safe).
      2. Checks for a .NET 9 (or newer) SDK.
      3. Runs `dotnet build` on Client\Simitone\Simitone.Windows\Simitone.Windows.csproj.
      4. Copies the build output into the deploy folder (default: .\SimitoneWindows)
         and also writes Simitone.Windows.exe next to Simitone.exe.

    The project's AssemblyName is "Simitone", so the real app host is Simitone.exe.
    Simitone.Windows.exe is a copy of the same app host (an apphost can be renamed;
    it always loads Simitone.dll from its own folder), kept so existing shortcuts work.

    If the deploy folder contains an old .NET Framework release of Simitone
    (e.g. the official v0.8.12 SimitoneWindows.zip), it is MOVED (not deleted) to
    "<folder>.legacy-<timestamp>" before the first deploy, so the two builds never mix.
    Both folders get a one-line .gitignore ("*") so they never show up in `git status`.

.EXAMPLE
    .\build.cmd                      # Release build -> .\SimitoneWindows
    .\build.cmd -Run                 # build, deploy, then start the game
    .\build.cmd -Configuration Debug # Debug build (better stack traces / debugger)
    .\build.cmd -Clean               # clean before building
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    # Deploy folder; relative paths are resolved against the repository root.
    [string]$OutputDir = 'SimitoneWindows',

    # Arguments passed to the game when -Run is used, e.g. -GameArgs '-3d'
    [string]$GameArgs = '',

    [switch]$Run,
    [switch]$Clean,
    [switch]$NoDeploy,
    # Never prompt (used by CI).
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

function Write-Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Fail([string]$msg) { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

# Runs a native command without letting stderr output abort the script
# (Windows PowerShell 5.1 turns redirected native stderr into terminating errors under 'Stop').
# Success is judged by $LASTEXITCODE afterwards.
function Invoke-Native([string]$Exe, [string[]]$ArgList) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Exe @ArgList } finally { $ErrorActionPreference = $prev }
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project  = Join-Path $RepoRoot 'Client\Simitone\Simitone.Windows\Simitone.Windows.csproj'
$BinDir   = Join-Path $RepoRoot "Client\Simitone\Simitone.Windows\bin\$Configuration\net9.0-windows"
if ([System.IO.Path]::IsPathRooted($OutputDir)) { $Deploy = $OutputDir } else { $Deploy = Join-Path $RepoRoot $OutputDir }

if (-not (Test-Path $Project)) { Fail "Cannot find $Project. Run this script from a Simitone checkout." }

# --- 1. FreeSO submodule --------------------------------------------------------------
$FreeSOMarker = Join-Path $RepoRoot 'FreeSO\TSOClient\tso.simantics\FSO.SimAntics.csproj'
if (-not (Test-Path $FreeSOMarker)) {
    Write-Step 'FreeSO submodule is not checked out - initialising it (one-time, needs network)'
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail 'git is not on PATH. Install Git for Windows and re-run.' }
    Invoke-Native git @('-C', $RepoRoot, 'submodule', 'update', '--init', '--recursive')
    if ($LASTEXITCODE -ne 0) { Fail 'git submodule update failed.' }
    if (-not (Test-Path $FreeSOMarker)) { Fail 'FreeSO submodule still missing after init.' }
}

# --- 2. .NET SDK ------------------------------------------------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$sdkOk = $false
if ($dotnet) {
    $sdks = Invoke-Native dotnet @('--list-sdks')
    foreach ($line in $sdks) {
        if ($line -match '^(\d+)\.') { if ([int]$Matches[1] -ge 9) { $sdkOk = $true } }
    }
}
if (-not $sdkOk) {
    Write-Host ''
    Write-Host 'A .NET 9 SDK (or newer) is required but was not found.' -ForegroundColor Yellow
    Write-Host '  Install it with:  winget install Microsoft.DotNet.SDK.9'
    Write-Host '  or download it from https://dotnet.microsoft.com/download/dotnet/9.0'
    if (-not $NonInteractive -and (Get-Command winget -ErrorAction SilentlyContinue)) {
        $answer = Read-Host 'Install the .NET 9 SDK now with winget? [y/N]'
        if ($answer -match '^(y|yes)$') {
            Invoke-Native winget @('install', '--id', 'Microsoft.DotNet.SDK.9', '-e')
            Write-Host 'Done. Open a NEW terminal (so PATH is refreshed) and run build.cmd again.' -ForegroundColor Yellow
        }
    }
    exit 1
}

# --- 3. Build -----------------------------------------------------------------------------
Push-Location $RepoRoot
try {
    if ($Clean) {
        Write-Step "dotnet clean ($Configuration)"
        Invoke-Native dotnet @('clean', $Project, '-c', $Configuration, '-nologo', '-v', 'minimal')
        if ($LASTEXITCODE -ne 0) { Fail 'dotnet clean failed.' }
    }
    Write-Step "dotnet build ($Configuration) - the first build restores NuGet packages and takes a few minutes"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-Native dotnet @('build', $Project, '-c', $Configuration, '-nologo', '-v', 'minimal')
    if ($LASTEXITCODE -ne 0) { Fail 'dotnet build failed (see errors above).' }
    Write-Host ("    build finished in {0:N0}s" -f $sw.Elapsed.TotalSeconds)
}
finally { Pop-Location }

$BuiltExe = Join-Path $BinDir 'Simitone.exe'
if (-not (Test-Path $BuiltExe)) { Fail "Build succeeded but $BuiltExe was not found." }
if (-not (Test-Path (Join-Path $BinDir 'Monogame\Windows\MonoGame.Framework.dll'))) {
    Write-Host 'WARNING: Monogame\Windows\MonoGame.Framework.dll is missing from the build output; the game will not start.' -ForegroundColor Yellow
}

# The app is framework-dependent and needs all three .NET 9 shared runtimes. ASP.NET Core is
# required because FreeSO's TSO client project (referenced by Simitone.Windows) pulls in its
# web-API server project. A newer SDK alone (e.g. .NET 10) can build the code but does not
# provide the 9.0 runtimes.
$runtimes = Invoke-Native dotnet @('--list-runtimes')
foreach ($fw in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App', 'Microsoft.AspNetCore.App')) {
    $found = $false
    foreach ($line in $runtimes) { if ($line -like "$fw 9.*") { $found = $true } }
    if (-not $found) {
        Write-Host "WARNING: runtime $fw 9.x is not installed; the game will not start." -ForegroundColor Yellow
        Write-Host '         Install the .NET 9 SDK (winget install Microsoft.DotNet.SDK.9), which includes it.'
    }
}

if ($NoDeploy) {
    Write-Step "Build output (not deployed): $BinDir"
    exit 0
}

# --- 4. Deploy ----------------------------------------------------------------------------
if (Test-Path $Deploy) {
    # A .NET Framework release has Simitone.Windows.exe.config and no Simitone.runtimeconfig.json.
    $isLegacy = (Test-Path (Join-Path $Deploy 'Simitone.Windows.exe.config')) -and
                -not (Test-Path (Join-Path $Deploy 'Simitone.runtimeconfig.json'))
    if ($isLegacy) {
        $backup = "$Deploy.legacy-" + (Get-Date -Format 'yyyyMMdd-HHmmss')
        Write-Step "Existing folder holds an old .NET Framework release - moving it to $backup"
        Move-Item -LiteralPath $Deploy -Destination $backup
        Set-Content -LiteralPath (Join-Path $backup '.gitignore') -Value '*'
    }
}
New-Item -ItemType Directory -Force -Path $Deploy | Out-Null
# Keep build output out of `git status` without touching the tracked .gitignore.
Set-Content -LiteralPath (Join-Path $Deploy '.gitignore') -Value '*'

Write-Step "Deploying to $Deploy"
# /E = include subfolders. No /MIR, so nothing that exists only in the deploy folder is ever deleted.
Invoke-Native robocopy @($BinDir, $Deploy, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:2', '/W:1') | Out-Host
if ($LASTEXITCODE -ge 8) { Fail "robocopy failed (exit $LASTEXITCODE). Is Simitone still running? Close it and retry." }
$global:LASTEXITCODE = 0

Copy-Item -LiteralPath (Join-Path $Deploy 'Simitone.exe') -Destination (Join-Path $Deploy 'Simitone.Windows.exe') -Force

$commit = ''
if (Get-Command git -ErrorAction SilentlyContinue) {
    $commit = Invoke-Native git @('-C', $RepoRoot, 'rev-parse', '--short', 'HEAD')
    $dirty  = Invoke-Native git @('-C', $RepoRoot, 'status', '--porcelain', '--untracked-files=no')
    if ($dirty) { $commit = "$commit+local-changes" }
}
Set-Content -LiteralPath (Join-Path $Deploy 'build-info.txt') -Value @(
    "configuration=$Configuration",
    "commit=$commit",
    "built=$(Get-Date -Format s)"
)

Write-Host ''
Write-Host "Done: $(Join-Path $Deploy 'Simitone.Windows.exe')" -ForegroundColor Green

if ($Run) {
    Write-Step 'Starting Simitone'
    $exe = Join-Path $Deploy 'Simitone.Windows.exe'
    if ($GameArgs) { Start-Process -FilePath $exe -WorkingDirectory $Deploy -ArgumentList $GameArgs }
    else { Start-Process -FilePath $exe -WorkingDirectory $Deploy }
}
exit 0
