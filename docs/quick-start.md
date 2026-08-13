# Quick start guide

This guide covers installation, normal use, configuration, testing, logs, and removal for ComfySentinel 1.5.0.

## 1. Requirements

- Valheim on Windows;
- BepInEx 5.4 installed and successfully launching;
- the `ComfySentinel.dll` release file.

ComfySentinel is client-side for normal play. A dedicated server does not need the DLL for sonar checks.

## 2. Install

1. Close Valheim.
2. Copy `ComfySentinel.dll` into:

   ```text
   <Valheim>/BepInEx/plugins/ComfySentinel.dll
   ```

3. Start Valheim and enter a world.
4. Check `BepInEx/LogOutput.log` for a line beginning with:

   ```text
   [ComfySentinel] Initialized with
   ```

On a default Steam installation, `<Valheim>` is usually:

```text
C:\Program Files (x86)\Steam\steamapps\common\Valheim
```

## 3. Use the camp checks

1. Pick up a newly spawned Fuling Totem from a pedestal or enemy drop.
2. Confirm the `CAMP CHECKS 3 / 9` card appears beneath the minimap.
3. Finish fighting or collecting as needed; pickup does not scan automatically.
4. Return within 64 metres of the newest eligible totem pickup position.
5. Press `V`.
6. Watch the enemy and loot rows refresh during the 10-second scan.
7. Read the final `FULINGS FOUND` or `CAMP CLEAR` state.

The default tracked categories are standard Fulings, Shamans, Brutes, loose Coins, and loose Black Metal. Container contents and player inventories are not included.

Each newly discovered totem grants three checks by default. Further eligible totems add checks up to the nine-check storage cap. A totem another player picked up and threw down cannot grant checks again. The saved gold summary remains beneath the ready state between checks. After the last check, press the card's `X` to close it or let the five-minute timer expire.

Checks survive death in the current world. If death interrupts an active scan, that scan is cancelled and its charge is restored. The card returns after respawn. Checks do not carry into another world or server.

## 4. Configure

After the first launch, edit:

```text
<Valheim>/BepInEx/config/comfy.mods.comfysentinel.cfg
```

Common changes:

```ini
[Controls]
PingHotkey = V

[Sonar]
MaxSonarCharges = 3
MaxStoredSonarCharges = 9
ScanRadius = 64

[Interface]
StateDuration = 10
FinalSummaryDuration = 300
```

Restart Valheim after editing the configuration.

## 5. Verify with the test fixture

Use a disposable local world or a world hosted by your own client.

1. Add `-console` to Valheim's Steam launch options.
2. Enter the world and press `F5`.
3. Run:

   ```text
   totemalert status
   totem 1
   ```

4. Close the console.
5. Pick up the displayed totem to arm the checks.
6. Press `V` and verify the fixture reports:

   - one standard Fuling;
   - one Fuling Shaman;
   - one Fuling Brute;
   - 12 Coins;
   - 8 Black Metal.

7. Kill or collect some fixture objects during or between checks and verify later pulses replace the old values.
8. Run `totem 1` again after collecting the first fixture totem and confirm the new totem adds three more checks.
9. Drop an already collected totem from inventory, pick it back up, and confirm the charge count does not increase.
10. Start a scan, die, respawn, and confirm the interrupted charge and prior completed result are preserved.
11. Clean up remaining tracked fixture objects:

   ```text
   totem clear
   ```

`totemalert` toggles a panel preview, while `totemalert on` and `totemalert off` set it explicitly. Previewing the panel does not arm a real sonar session.

The fixture is rejected on an ordinary multiplayer client. It intentionally spawns networked Valheim objects, so only a local-world player or the host can run it. `totem clear` removes objects still tracked by the fixture; it cannot undo loot already picked up or ordinary world changes caused during the test.

## 6. Read the logs

Open:

```text
<Valheim>/BepInEx/LogOutput.log
```

A completed scan writes one summary similar to:

```text
[ComfySentinel] Camp scan complete origin=(0.0,0.0,0.0) distance=12.3m radius=64.0m goblins=0 shamans=0 brutes=0 fulings=0 coins=7 blackMetal=11 charges=2 pulses=11 scanCpu=0.420ms source=local-zdo.
```

`pulses` is the exact number of successful local snapshots. `scanCpu` is their combined scanner CPU time, not the 10-second wall-clock UI duration.

Useful console command:

```text
totemalert status
```

It prints the plugin version, session state, charges, radius, hotkey, UI durations, and number of tracked fixture objects.

## 7. Troubleshoot

### No card after pickup

- Confirm the loose item was a `GoblinTotem` and the local player successfully added it to inventory.
- A totem that has already been through any player's inventory is intentionally ineligible.
- Confirm the initialization line is present in `LogOutput.log`.
- Look for a ZDO sector-binding compatibility error.
- Remove older duplicate ComfySentinel DLLs from other BepInEx plugin folders.

### Pressing the key does nothing

- Close the F5 console and any text-entry field.
- Wait until the current result or warning state returns to Ready.
- Confirm the configured key with `totemalert status`.
- Confirm at least one check remains.

### Out of range

The distance is measured from the newest eligible totem's loose-item coordinates, not the card, player spawn, item stand, or center of the visible village. Return closer and try again; the rejected attempt does not consume a check.

### Counts differ from the server's world

This is expected when an object is not currently hydrated on the client or its removal has not yet appeared in the local ZDO table. The sonar is intentionally local and does not query the server. It is a player aid, not server-authoritative proof.

### Scan failed

Review `LogOutput.log`. A startup failure usually indicates that the installed Valheim build changed private `ZDOMan` fields. A mid-session failure means the local sector table was temporarily unavailable. Do not repeatedly press the key until the underlying client state is healthy.

## 8. Update or remove

To update, close Valheim and replace the existing DLL with the newer release.

To uninstall:

1. Close Valheim.
2. Delete `BepInEx/plugins/ComfySentinel.dll`.
3. Optionally delete `BepInEx/config/comfy.mods.comfysentinel.cfg`.

ComfySentinel does not maintain a database or save normal sonar state into the world.
