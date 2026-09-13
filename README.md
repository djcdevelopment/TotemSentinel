# TotemSentinel

[![Development: AI-Assisted](https://img.shields.io/badge/Development-AI--Assisted-blueviolet.svg)](https://github.com/djcdevelopment/TotemSentinel)
[![Valheim: 1.0 Compatible](https://img.shields.io/badge/Valheim-1.0%20Compatible-brightgreen.svg)](https://github.com/djcdevelopment/TotemSentinel)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

TotemSentinel is a standalone client-side BepInEx mod for Valheim that turns newly discovered Fuling Totems into a tactical, player-controlled camp-check radar and wide treasure search.

The first successful pickup of a world- or enemy-spawned Fuling Totem grants three camp checks (bankable up to nine). It does not scan immediately, allowing players to finish combat before choosing when to sweep.

---

## The Flow: Live Counts Become The Final Answer

> **Values replace • They do not accumulate.**

![TotemSentinel Live Counts Clearing Flow](https://raw.githubusercontent.com/djcdevelopment/TotemSentinel/main/docs/images/live-clear-flow.png)

1. **Live Pulse (3s)**: Tapping `V` spends one check and initiates a 3-second live pulse beneath the minimap.
2. **Real-time Reaction**: As enemies fall or loot is gathered, live sector memory updates dynamically.
3. **Decisive Snapshot**: The final pulse replaces the counter with an authoritative `CAMP CLEAR` or enemy tally.
4. **Stable Final Summary**: The final results linger cleanly without rushing the player, dismissible with `[X]` or auto-closing after 5 minutes.

---

## Signature Feature: Greed's Gambit

Hold **`LeftShift`** while pressing **`V`** to trigger **Greed's Gambit**—a high-stakes, 3x wide-area search for wealth across the plains.

```
┌──────────────────────────────────────┐     ┌──────────────────────────────────────┐
│        GREED'S GAMBIT (3x WIDE)      │ ──▶ │         "AT A SINGLE SCRATCH"        │
│   192m Search: Coins, Metal, Jewels  │     │   Any damage triggers Horde Retribution  │
└──────────────────────────────────────┘     └──────────────────────────────────────┘
```

- **3x Wide Scan Radius**: Expands loot radar up to 192 metres, detecting buried or hidden `Coins`, `BlackMetalScrap`, `Rubies`, `Amber`, `AmberPearls`, `SilverNecklaces`, and `Fuling Totems`.
- **The Curse of the Totem (35s)**: For 35 seconds following a Greed scan, the player is cursed by ancient avarice.
- **Single-Scratch Retribution**: Taking **any damage** while cursed immediately alerts (`BaseAI.Alert()`) and enrages every creature across the 192m sector to hunt the player down!

---

## Features

- **Player Agency First**: Totems grant checks; they never force an intrusive scan mid-combat.
- **Greed's Gambit Risk/Reward**: 3x wide loot detection balanced by a 35-second vulnerability window.
- **Death Safe**: Earned checks survive player death; interrupted in-progress scans are refunded.
- **Minimap Anchored**: Compact card positioned beneath the circular minimap (`MapCorners`)—no center-screen spam.
- **Comprehensive Enemy Breakdown**: Separate live counts for standard Fulings, Shamans, and Brutes.
- **Loose Loot Tracking**: Accurately counts loose coins, black metal scrap stacks, and precious gems in tall grass.
- **Zero Network Lag**: Operates exclusively on local, already-hydrated client ZDOs. 0 server RPCs, 0 desync risk.
- **Hot-Path Optimized**: Allocation-free ZDO sector iteration; no LINQ or physics overhead.

---

## Controls

| Key | Action |
| :--- | :--- |
| **`V`** | Standard Camp Check (64m radius: Fulings & local loot). |
| **`Shift + V`** | **Greed's Gambit** (192m radius: 3x Wide Loot Scan + 35s Cursed Retribution). |

---

## Configuration

Settings are saved in `BepInEx/config/djcdevelopment.totemsentinel.cfg`:

| Setting | Default | Description |
| :--- | :---: | :--- |
| `MaxSonarCharges` | `3` | Camp checks granted by each newly discovered totem. |
| `MaxStoredSonarCharges` | `9` | Maximum checks banked across totems. |
| `ScanRadius` | `64` | Standard camp scan radius in metres. |
| `PingHotkey` | `V` | Key used to spend a check. |
| `GreedMultiplier` | `3.0` | Radius multiplier for Greed's Gambit wide searches (192m). |
| `GreedDuration` | `35` | Seconds that the Greed retribution curse remains active. |
| `GreedModifierKey` | `LeftShift` | Key held with `PingHotkey` to initiate Greed's Gambit. |
| `StateDuration` | `10` | Seconds that scanning and result states remain visible. |
| `FinalSummaryDuration` | `300` | Seconds before the final summary auto-closes. |

---

## In-Game Test Harness

Enable `-console` in Steam launch options, enter any world, and press `F5`:

- `totemalert`: Toggles UI card preview.
- `totemalert status`: Displays current session charges and settings.
- `totem 1`: Spawns an isolated test fixture (pedestal totem, 3 fulings, coins, and black metal).
- `totem clear`: Destroys tracked fixture objects.

---

## AI Disclosure & Transparency

In compliance with community and platform guidelines:
- **Development**: Architected, modernized for Valheim 1.0.12, and tuned with AI assistance.
- **Design & Testing**: Fully audited with Cecil reflection against native game binaries and verified locally on Valheim 1.0.12.

---

## License

MIT License. Crafted with care for the Valheim community.
