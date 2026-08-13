# ComfySentinel

ComfySentinel is a standalone client-side BepInEx mod for Valheim that turns a picked-up Fuling Totem into a limited camp-check ability.

Picking up a `GoblinTotem` grants three checks. It does not scan immediately, so players can finish an active fight before pressing `V`. Each check searches the original camp radius and reports remaining Fulings plus loose coins and black metal.

## Features

- Three configurable camp checks per picked-up Fuling Totem.
- Compact state card beneath the minimap instead of center-screen combat messages.
- Separate counts for Fulings, Shamans, and Brutes.
- Loose `Coins` and `BlackMetalScrap` stack totals in the same scan.
- Ten-second scanning, result, and warning stages by default.
- One local-memory snapshot per second while scanning, with live replacement counts and no scan RPCs.
- Persistent gold summary of the previous check.
- Dismissible final summary that closes automatically after five minutes.
- Detailed logs for session arming, scan results, range failures, charge use, and scan duration.
- Allocation-free ZDO sector iteration in the scanner hot path; no LINQ, physics queries, or per-scan reflection.

ComfySentinel only counts memory-hydrated ZDOs within the configured radius. Loot inside containers and player inventories is intentionally excluded.

Live pulses never request sectors, write ZDOs, claim ownership, or contact the server. On multiplayer clients, results are limited to state Valheim has already hydrated naturally. The `totem 1` test harness is the sole feature that intentionally creates networked world objects when run by a host.

## Installation

1. Install Valheim and BepInEx 5.4.
2. Download `ComfySentinel.dll` from the latest GitHub release.
3. Copy it into `Valheim/BepInEx/plugins/`.
4. Start Valheim.

The generated configuration is written to `BepInEx/config/comfy.mods.comfysentinel.cfg`.

## Usage

1. Pick up a Fuling Totem to arm the camp checks.
2. Return within the configured camp radius when ready.
3. Press `V` to perform a check.

The default flow is:

```text
CAMP CHECKS 3 / 3
        ↓ V
SCANNING + live local counts
        ↓
FULINGS FOUND / CAMP CLEAR
        ↓
CAMP CHECKS 2 / 3 + previous result
```

After the third check, the previous result remains visible with a close button and a five-minute automatic timeout.

## Configuration

| Setting | Default | Description |
| --- | ---: | --- |
| `MaxSonarCharges` | `3` | Checks granted by each totem. |
| `ScanRadius` | `64` | Camp scan radius in metres. |
| `PingHotkey` | `V` | Key that spends a check. |
| `StateDuration` | `10` | Seconds for scanning, result, and warning states. |
| `FinalSummaryDuration` | `300` | Seconds before the final summary closes. |

## Test harness

Start Valheim with `-console`, enter a local world or a world hosted by your client, and press F5.

- `totemalert` toggles a panel preview.
- `totemalert status` prints the current state and settings.
- `totem 1` creates a non-persistent test fixture with a pickable totem, all three Fuling variants, 12 coins, and 8 black metal.
- `totem clear` removes remaining tracked fixture objects.

The spawn command is rejected on ordinary multiplayer clients. Use the fixture only in a disposable test world; picked-up loot and normal world changes are not undone by `totem clear`.

## Building

The project targets .NET Framework 4.8 and references assemblies from a local Valheim/BepInEx 5.4 installation.

```powershell
dotnet build .\ComfySentinel.csproj -c Release -p:ValheimDir="C:\path\to\Valheim"
```

If `ValheimDir` is omitted, the project uses the standard Windows Steam location:

```text
C:\Program Files (x86)\Steam\steamapps\common\Valheim
```

No Valheim, Unity, Harmony, or BepInEx assemblies are redistributed in this repository.

## Compatibility

ComfySentinel 1.4.0 was built against:

- Valheim `0.221.12`
- BepInEx `5.4.23.3`
- .NET Framework `4.8`

Private Valheim ZDO storage is bound once at startup. If a future game update changes those internals, ComfySentinel logs the compatibility failure and does not install its gameplay patch.

## License

[MIT](LICENSE)
