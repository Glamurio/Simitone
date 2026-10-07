# Building and running Simitone (this fork)

## Quick rebuild

```
build.cmd                         # Release build, deployed to .\SimitoneWindows
build.cmd -Run                    # ...then start the game
build.cmd -Configuration Debug    # Debug build (better stack traces, debugger-friendly)
build.cmd -Run -GameArgs "-3d"    # pass arguments to the game
build.cmd -Clean                  # clean first
```

`build.cmd` runs `tools\build.ps1`, which:

1. Initialises the `FreeSO` submodule **only if it has never been checked out**. An existing
   checkout is never reset, so local FreeSO commits are safe.
2. Checks for a .NET 9 (or newer) SDK. If none is found it prints install instructions and
   offers `winget install Microsoft.DotNet.SDK.9`.
3. Runs `dotnet build Client\Simitone\Simitone.Windows\Simitone.Windows.csproj -c <Configuration>`.
4. Copies `Client\Simitone\Simitone.Windows\bin\<Configuration>\net9.0-windows\` into
   `SimitoneWindows\` (robocopy `/E`, which never deletes anything) and writes
   `Simitone.Windows.exe` next to `Simitone.exe`.

### One-time prerequisites

| Requirement | Why | Notes |
|---|---|---|
| Git for Windows | Fetches the FreeSO submodule (and nested FSOMonoGame / FSOMina.NET / assimp-net). | |
| .NET 9 SDK | All projects target `net9.0` / `net9.0-windows` since upstream PR #55. | The .NET 5/6 SDKs are not enough. The SDK also installs the three runtimes the game needs: .NET, Windows Desktop and **ASP.NET Core** 9. ASP.NET Core is needed because `Simitone.Windows` references FreeSO's TSO client, which references its web-API server project (see the recon report, A.2 and G.11). |
| Network on first build | NuGet restore. | Later builds are incremental. |
| The Sims Complete Collection | Game data at run time. | Found automatically: `..\The Sims\`, then registry `HKLM\SOFTWARE\Maxis\The Sims\InstallPath`, then Steam Legacy Collection, then `C:\Program Files (x86)\Maxis\The Sims\`. Override with `-path<dir>`. |

### Executable name

The project's `AssemblyName` is `Simitone`, so the .NET app host is `Simitone.exe`.
`Simitone.Windows.exe` is a copy of the same app host. An app host always loads `Simitone.dll`
from its own folder, so both names start the same game. The old .NET Framework releases
(v0.8.x) used `Simitone.Windows.exe` as their real name.

### The old release in `SimitoneWindows\`

If `SimitoneWindows\` holds a .NET Framework release, `build.ps1` moves it to
`SimitoneWindows.legacy-<timestamp>\` before the first deploy. It is moved, not deleted.
Detection: `Simitone.Windows.exe.config` is present and `Simitone.runtimeconfig.json` is absent.
The official v0.8.12 release can also be downloaded again from
https://github.com/riperiperi/Simitone/releases.

Both folders get a one-line `.gitignore` (`*`), so they never show up in `git status`. The
repository's own `.gitignore` is not modified.

## Running

* Start `SimitoneWindows\Simitone.Windows.exe` (or `Simitone.exe`).
* Useful arguments, parsed in `Client\Simitone\Simitone.Windows\Program.cs`:

  | Argument | Effect |
  |---|---|
  | `-3d` | 3D mode |
  | `-ide` | Also open Volcanic (the BHAV/object IDE and debugger) |
  | `-gl` / `-ogl`, `-dx` / `-dx11` | Graphics backend (DirectX 11 is the default) |
  | `-aa` | Anti-aliasing |
  | `-touch` | Touch / software-keyboard mode |
  | `-path<dir>` | Game install path |
  | `-nosound` | Disable sound |
  | `-jit` | Scan for AOT-compiled SimAntics modules (none ship today) |
  | `-lang<N>`, `-hz<N>` | Language code, refresh rate |
  | `nosimantics-exc` (anywhere in args) | Suppress SimAntics exception dialogs |

* User data, saves and config: `Documents\Simitone\`. On first use, the original `UserData`
  neighbourhood is copied there. Simitone never writes into the original game folder.

### Runtime requirements on a machine without the SDK

This applies, for example, to the CI zip. You need the **.NET Desktop Runtime 9** *and* the
**ASP.NET Core Runtime 9**:

```
winget install Microsoft.DotNet.DesktopRuntime.9
winget install Microsoft.DotNet.AspNetCore.9
```

Without the ASP.NET Core runtime, the app host refuses to start and asks you to install the
missing `Microsoft.AspNetCore.App` framework.

## Debugging

* Open `Client\Simitone\Simitone.sln` in Visual Studio 2022 (17.12+) or Rider. Set
  `Simitone.Windows` as the startup project. Launch profiles in `Properties\launchSettings.json`:
  normal, `-3d`, `-ide -3d`.
* Volcanic (`-ide`) gives BHAV breakpoints and an entity inspector. In-game, Shift+click on a
  pie-menu entry opens its BHAV.
* In-game keys:
  * F11 runs `CollisionTestUtils.VerifyAllCollision` (`UILotControl`).
  * F8 makes a lot thumbnail (`UILotControl`).
  * Ctrl+Shift+C opens the cheat box (`UICheatTextbox`).
  * 1 / 2 / 3 set speed, P pauses, 0 steps one tick (`TS1GameScreen`).

## CI

`.github/workflows/windows-build.yml` runs the same `tools/build.ps1` on `windows-latest` for
every push. It uploads `SimitoneWindows.zip` as a workflow artifact and as a rolling
pre-release named `dev-build-<branch>` (see the repository's Releases page).

Actions are enabled on this fork: the first run built `ac284ba` in about 5 minutes. If they
are ever disabled, re-enable them in the fork's **Actions** tab.
