# ComfySentinel data-flow diagrams

This document describes ComfySentinel 1.5.0 as implemented. The normal sonar path is entirely inside the Valheim client: it reads the ZDOs that Valheim has already hydrated and does not request data from the server.

## System context

```mermaid
flowchart LR
    player([Player])
    admin([Local host / tester])
    server[(Valheim server or host)]

    subgraph client[Valheim client process]
        game[Valheim gameplay]
        zdo[(Hydrated local ZDO sectors)]
        sentinel[ComfySentinel]
        hud[Camp-check HUD]
        log[(BepInEx LogOutput.log)]
    end

    player -->|pick up totem / press hotkey| game
    game -->|successful eligible pickup| sentinel
    player -->|hotkey input| sentinel
    server -.->|ordinary Valheim world replication| game
    game -->|hydrates and maintains| zdo
    zdo -->|read-only local snapshots| sentinel
    sentinel -->|state and live counts| hud
    sentinel -->|session, warning, and final scan records| log
    hud -->|visual feedback| player

    admin -->|totem 1 / totem clear| sentinel
    sentinel -->|test fixture only: spawn / destroy| game
    game <-->|ordinary Valheim object replication| server
```

The dotted server-to-client flow is owned by Valheim. ComfySentinel does not initiate it, force a sector to load, claim ZDO ownership, or send a sonar RPC.

## Level 1: normal gameplay flow

```mermaid
flowchart TD
    source[Pedestal or enemy produces loose GoblinTotem]
    pickup[Local player successfully picks up ItemDrop]
    provenance{ItemData.m_pickedUp was false?}
    reject[Do not grant: player-thrown or previously held]
    origin[Capture loose-item world origin]
    session[Add per-totem charges up to storage cap]
    ready[HUD: CAMP CHECKS ready]
    key[Player presses configured hotkey]
    guards{Input and session guards pass?}
    range{Player within configured radius?}
    warning[HUD warning; charge retained]
    scan[Start local scan window]
    localzdo[(ZDOMan.m_objectsBySector)]
    live[Replace live HUD counts]
    final[Store final snapshot and write one completion log]
    result{Any Fulings?}
    found[HUD: FULINGS FOUND]
    clear[HUD: CAMP CLEAR]
    charges{Charges remain?}
    readyagain[HUD returns to ready with last summary]
    complete[HUD: CHECKS COMPLETE]
    close[Dismiss with X or five-minute timeout]
    death{Player dies?}
    refund[Cancel active check and restore its charge]
    suspended[Preserve session; hide HUD]
    respawn[Living local player respawns]
    worldexit[World changes or closes]
    reset[Clear session]

    source --> pickup --> provenance
    provenance -->|no| reject
    provenance -->|yes| origin --> session --> ready
    ready --> key --> guards
    guards -->|no charges / UI busy / input blocked| warning
    guards -->|yes| range
    range -->|no| warning
    range -->|yes| scan
    scan --> death
    death -->|yes| refund --> suspended --> respawn --> ready
    death -->|no: initial, one-second pulses, final| localzdo
    localzdo --> live
    live -->|until scan timer ends| localzdo
    live --> final --> result
    result -->|yes| found
    result -->|no| clear
    found --> charges
    clear --> charges
    charges -->|yes| readyagain --> key
    charges -->|no| complete --> close
    ready -.-> worldexit
    suspended -.-> worldexit
    complete -.-> worldexit
    worldexit --> reset
```

## Level 2: one camp check

```mermaid
sequenceDiagram
    actor Player
    participant Plugin as ComfySentinelPlugin
    participant Scanner as SonarScanner
    participant ZDO as Local ZDO sector array
    participant UI as SonarAbilityPanel
    participant Log as ZLog

    Player->>Plugin: Press configured hotkey
    Plugin->>Plugin: Validate session, charges, UI state, and start range
    Plugin->>Scanner: TryScan(origin, radius)
    Scanner->>ZDO: Read the 5x5 sector window
    ZDO-->>Scanner: Already-hydrated ZDO references
    Scanner-->>Plugin: Initial count snapshot
    Plugin->>Plugin: Consume one charge
    Plugin->>UI: BeginScan(snapshot)

    loop Approximately once per second for StateDuration
        Plugin->>Scanner: TryScan(origin, radius)
        Scanner->>ZDO: Read the same local sector window
        Scanner-->>Plugin: Replacement count snapshot
        Plugin->>UI: UpdateScan(snapshot)
    end

    alt Player remains alive
        Plugin->>Scanner: Final TryScan(origin, radius)
        Scanner-->>Plugin: Authoritative final snapshot
        Plugin->>Log: One completion record and total scanner CPU time
        Plugin->>UI: CompleteScan(final snapshot)
        UI-->>Player: Found or clear state, then remaining checks / completion
    else Player dies during the window
        Plugin->>Plugin: Cancel window and restore one charge
        Plugin->>UI: Discard incomplete live result and suspend
        Player->>Plugin: Respawn in the same world
        Plugin->>UI: Resume prior completed result and ready state
    end
```

One charge is consumed after the initial snapshot succeeds. Intermediate values are replacements, not a cumulative total. If local ZDO access fails before the first snapshot, no charge is consumed. If it fails after the window starts, the already-consumed charge is not restored. Death during the window is a deliberate exception: the incomplete check is cancelled and its charge is refunded.

## Data stores and payloads

| Store or payload | Owner | Access | Contents |
| --- | --- | --- | --- |
| Hydrated ZDO sector array | Valheim | ComfySentinel reads only | Active client-known world objects indexed by sector |
| Loose totem provenance | Valheim item/ZDO data | ComfySentinel reads only | `m_pickedUp` distinguishes a first world pickup from a re-dropped inventory item |
| Sonar session state | ComfySentinel process memory | Read/write, client only | World UID, origin, stacked charges, death suspension, scan timers, pulse count, CPU ticks |
| Current scan result | ComfySentinel process memory | Read/write, client only | Standard Fulings, Shamans, Brutes, Coins, Black Metal |
| Generated config | BepInEx | Read at runtime | Charge, radius, key, and UI-duration settings |
| `LogOutput.log` | BepInEx | Append through `ZLog` | Initialization, session, rejection, final result, and errors |
| Test fixture object list | ComfySentinel process memory | Read/write during test commands | References to the seven spawned fixture objects |

## Network trust boundary

Normal sonar checks do not cross the network boundary. They do not:

- invoke an RPC;
- create, mutate, or destroy a ZDO;
- ask Valheim to load a sector;
- claim network ownership;
- use a server query or remote administrative API.

The `totem 1` and `totem clear` test commands are the explicit exception. On a local or client-hosted world they create or destroy ordinary Valheim network objects, so Valheim may replicate those fixture changes to connected players.
