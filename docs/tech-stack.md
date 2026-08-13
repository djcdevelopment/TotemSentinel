# Technical stack

ComfySentinel is a single client-side BepInEx plugin built for the classic Valheim Mono runtime. It does not ship a server component, external service, database, or web UI.

## Stack summary

| Layer | Technology | Role |
| --- | --- | --- |
| Language | C# | Plugin, scanner, HUD, Harmony patch, and test commands |
| Target framework | .NET Framework 4.8 (`net48`) | Compatible target for BepInEx 5 and Valheim's Mono runtime |
| Mod loader | BepInEx 5.4 | Plugin lifecycle, configuration, and assembly loading |
| Patch engine | Harmony (`0Harmony`) | Hooks `Humanoid.Pickup` without replacing Valheim code |
| Game API | `assembly_valheim.dll` | Player, Humanoid, ItemDrop, Pickable, ZDO, ZoneSystem, ZNetScene, console, and HUD integration |
| Engine API | UnityEngine | Input, transforms, time, IMGUI, effects, math, and test fixture objects |
| UI | Unity immediate-mode GUI (`OnGUI`) | Compact card positioned beneath the small minimap |
| Local data source | `ZDOMan.m_objectsBySector` | Read-only spatial snapshots over already-hydrated client state |
| Logging | Valheim `ZLog` / BepInEx log output | Startup, session, result, warning, and diagnostic records |
| Build | SDK-style MSBuild through `dotnet build` | Produces the standalone `ComfySentinel.dll` |
| Distribution | GitHub source/releases | Manual DLL installation into BepInEx |

## Runtime dependencies

The project references these assemblies from an existing Valheim installation; they are not redistributed:

- `BepInEx.dll`
- `0Harmony.dll`
- `assembly_valheim.dll`
- `assembly_utils.dll`
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.InputLegacyModule.dll`
- `UnityEngine.IMGUIModule.dll`
- `UnityEngine.PhysicsModule.dll`
- `UnityEngine.TextRenderingModule.dll`
- `UnityEngine.UI.dll`

`UnityEngine.PhysicsModule` is used by the host-only test fixture to add its interaction collider. The production sonar scanner does not issue Unity physics queries.

## Source layout

| Path | Responsibility |
| --- | --- |
| `ComfySentinelPlugin.cs` | Lifecycle, configuration, session state, input guards, pulse timing, and logging |
| `Patches/TotemPickupPatch.cs` | Detects the first successful local pickup of a never-before-picked-up Fuling Totem |
| `Services/SonarScanner.cs` | Read-only, allocation-free hot-path ZDO sector scan |
| `HUD/SonarPresenter.cs` | Connects scan events to the UI and guarded local wishbone effect |
| `HUD/SonarAbilityPanel.cs` | UI state machine, responsive positioning, live summary, and dismissal |
| `Commands/TotemAlertCommands.cs` | UI preview/status command and host-only disposable fixture |
| `ComfySentinel.csproj` | `net48` target and local Valheim/BepInEx reference paths |

## Build environment

Requirements:

- Windows or another environment capable of targeting .NET Framework 4.8;
- .NET SDK with MSBuild support for `net48`;
- Valheim installed locally;
- BepInEx 5.4 installed into that Valheim directory.

Build with the default Steam location:

```powershell
dotnet build .\ComfySentinel.csproj --configuration Release
```

Or supply another Valheim directory:

```powershell
dotnet build .\ComfySentinel.csproj --configuration Release `
  -p:ValheimDir="D:\Games\Valheim"
```

The output is `bin/Release/ComfySentinel.dll`. Reference assemblies have `Private=false`, so the build output does not copy Valheim, Unity, Harmony, or BepInEx binaries.

## Compatibility profile

Version 1.5.0 was built against:

- Valheim `0.221.12`;
- BepInEx `5.4.23.3`;
- .NET Framework 4.8.

The most version-sensitive integration is the one-time Harmony `FieldRef` binding to `ZDOMan.m_objectsBySector`, `m_width`, and `m_halfWidth`. If those private fields change, initialization logs an error and the plugin deliberately does not install its gameplay patch.

## Deliberate exclusions

The production scan path does not use:

- LINQ;
- Unity physics overlap or raycast queries;
- per-scan reflection;
- coroutines or background threads;
- a server-side plugin;
- HTTP, sockets, telemetry, or external storage;
- RPCs or ZDO mutations.

The HUD uses Unity IMGUI and formats display strings while drawing. The allocation-free guarantee applies to the scanner's spatial iteration, not to the entire Unity UI frame.
