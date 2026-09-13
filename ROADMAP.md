# TotemSentinel & Arcane Optics: Design Manifesto & Master Roadmap

**Author & Studio**: `djcdevelopment`  
**Ecosystem**: Valheim 1.0.12+ Sovereign Modding Suite  
**Status**: Living Architecture & Product Roadmap

---

## 1. Executive Summary & Core Philosophy

This document captures the master design vision, economic systems, and expansion roadmap born from the development of **TotemSentinel** and **Arcane Sight**.

### The Three Tenets:
1. **Live Counts Become The Final Answer**:
   * *Values replace; they do not accumulate.* Scanning does not spam or leave stale noise on screen. A rhythmic pulse replaces state until a definitive, calm snapshot locks into place.
2. **Diegetic Integration over Cheat Toggles**:
   * Utility is not granted for free through dev consoles. Radar and x-ray optics must be earned, crafted, fueled, and grounded in authentic Norse/Dvergr lore.
3. **Anti-Inflation Resource Sinks**:
   * Late-game survival suffers from resource saturation (chests overflowing with resin, cores, and coins). Every high-tier optical tool must act as a resource sink that rewards active player labor.

---

## 2. Feature Architecture: Shipped & Active

### 2.1 TotemSentinel v1.5.0
* **Camp Radar Pulse**: Picking up a newly discovered Fuling Totem banks 3 to 9 camp checks. Pressing `V` executes a 3-second live pulse beneath the minimap (`MapCorners`).
* **Zero-Alloc Hot Path**: Memory-hydrated ZDO queries with dynamic sector grids (`sectorRadius = Mathf.Clamp(...)`). Zero RPCs, zero server traffic, zero desync.
* **Greed's Gambit (3x Wide Loot Radar)**:
  * Holding `LeftShift + V` triggers a 192m wide search for `Coins`, `BlackMetalScrap`, `Rubies`, `Amber`, `AmberPearls`, `SilverNecklaces`, and loose `Fuling Totems`.
  * **Curse of the Totem (35s)**: Arms a 35-second vulnerability window.
  * **"At a Single Scratch" Retribution**: Taking *any* damage while cursed immediately alerts (`BaseAI.Alert()`) and enrages all creatures across the 192m sector to hunt the player.

---

## 3. The Horizon: Gnomish Goggles & The Pure Resin Economy

```
┌────────────────────────────────────────────────────────────────────────┐
│                        THE PURE RESIN HARVEST                          │
├────────────────────────────────────────────────────────────────────────┤
│  🌾 FARMING (Harvesting Carrots/Barley/Onions) ──▶ 5% Pure Resin ✨    │
│  🍳 COOKING (Baking pies, brewing mead)        ──▶ 8% Pure Resin ✨    │
│  🔨 CRAFTING (Forging weapons, building bases) ──▶ 5% Pure Resin ✨    │
│  🪓 CHOPPING (Felling Birch, Oak, Pine trees)  ──▶ 10% Pure Resin ✨   │
├────────────────────────────────────────────────────────────────────────┤
│   AFK Greydwarf Grinder Pits                   ──▶ 0% (Anti-Cheese)    │
└────────────────────────────────────────────────────────────────────────┘
```

### 3.1 The "Pure Resin" Solution to AFK Farms
* **The Problem**: In vanilla Valheim, digging a pit under a Greydwarf spawner with campfires generates 5,000 resin and eyes per hour completely AFK. Using standard resin as fuel breaks the economy on day one.
* **The Solution**: The optical engine requires **Pure Resin**—a rare, shimmering amber drop earned exclusively through **active, intentional player gameplay**:
  * Farming & crop harvesting (`Pickable.Interact`)
  * Cauldron cooking & mead fermentation (`InventoryGui.DoCrafting`)
  * Smelting & workbench crafting
  * Manual timber felling (`TreeLog.Destroy`)
* **The Asymmetric Multiplayer Economy**:
  * Veterans in Carapace armor hunting Mistlands dungeons cannot run their high-frequency lenses on mob-grinder trash.
  * Late-starting players in the Meadows cultivating fields and felling timber become the essential fuel barons of the server, trading Pure Resin for advanced metals and food.

### 3.2 In-Memory Crafting: The Dvergr Monocle
* **Zero-Bloat Implementation**:
  * Clones the native `HelmetDverger` prefab at runtime via C# (`ObjectDB.instance.m_recipes`).
  * Requires no external 50MB asset bundles or heavy third-party framework dependencies.
  * Fully multiplayer safe: vanilla clients and dedicated servers render a standard Dvergr circlet; modded clients unlock the multi-spectrum HUD.
* **4-Tier Workbench Upgrade Progression (`m_quality` 1 - 4)**:
  * **Tier 1 (Surveyor's Lens)**: Highlights containers, fermenters, beehives, and portal IDs. (Fuel: Pure Resin + Dandelions).
  * **Tier 2 (Delver's Monocle)**: Pierces sunken crypt stone to outline iron scrap piles, submerged chests, and ancient bark. (Fuel: Surtling Cores + Greydwarf Eyes).
  * **Tier 3 (Seeker's Spectacles)**: Replaces the clunky Wishbone audio ping with true **3D visual contours of buried Silver Veins** and Dragon Eggs. (Fuel: Freeze Glands + Wolf Fangs).
  * **Tier 4 (Avarice Sovereign)**: Unlocks full Greed's Gambit multi-spectrum radar (Fuling Totems, Tar, Dvergr extractors, Infested Mine seal fragments up to 192m). (Fuel: Refined Eitr + Pure Resin).

---

## 4. The Cinematic Trailer: "The Totem's Gambit"

A four-act cinematic showcase script designed to demonstrate the stakes, game-feel, and atmospheric integration of TotemSentinel and Arcane Sight.

### The Storyboard:
1. **Act I: The Hunger (Tension & Scarcity)**
   * Low camera gliding through the wind-swept golden barley of the Plains.
   * Food buff HUD blinking: Lox Meat Pie (3:00), Serpent Stew (2:00), Blood Pudding (0:58 - blinking red).
   * Objective: A fortified Fuling village feast table loaded with mead and roasted meat, guarded by **3 Totems on stone pedestals**, 3 hooded Shamans, and 2 patrolling Brutes under a bright, deceptive noon sun.
2. **Act II: The First Inscription (The Gathering Storm)**
   * Player ambushes Shaman #1 and snatches **Totem #1**.
   * *The World Shift*: Sky instantly curdles into thick, slate-grey storm clouds. Wind howls through the grass.
   * *HUD*: Minimap card clicks alive: `CAMP CHECKS: 3 / 9 · SCANNING`.
3. **Act III: The Deluge & Greed (The 35s Curse)**
   * Player slides into the medicine hut, toggling Arcane Sight to spot stamina mead in purple and gold through the wooden walls.
   * Player cuts down Shaman #2 and rips **Totem #2** off its stand.
   * *The World Shift*: Heavy rain becomes a violent thunderstorm; day snaps to midnight blackness lit only by lightning and a thin sliver of moon.
   * Player takes a grazing hit (12 damage). Minimap card flares flashing red: `CURSED! (35s) · HORDE ENRAGED & HUNTING`.
4. **Act IV: The Sun Breaks (The Final Cleansing)**
   * Exhausted combat duel in the mud. Shaman #3 and the final Brute fall. Player lifts **Totem #3** skyward.
   * *The Climax*: The storm abruptly halts mid-air. Clouds tear apart in golden volumetric god-rays. Noon sunlight floods the village.
   * Player presses `V`: 3-second sweep concludes with emerald **`CAMP CLEAR · COINS 48 · METAL 14`**.
   * Final shot: Player feasting at the table with the three glowing totems lined up as trophies.

### Planned Tooling: `totem film_set`
* A developer console command in `TotemAlertCommands.cs` that automatically builds, dresses, and stages the entire banquet table, potion hut, 3 pedestals, Shamans, Brutes, and loot in a single click for instant 60fps recording.
* Automated weather conductor hooks tied to totem pickup events (`Clear` ➔ `Darklands_storm` ➔ `ThunderStorm` ➔ `Clear`).

---

## 5. Sovereign Integration Seams (The Creator OS Bridge)

To ensure these standalone mods seamlessly hand off to larger narrative systems when present:
* **Native ZDO Persistence**:
  * `comfy.rune.glow` (Color preset index)
  * `comfy.rune.title` (Inscribed name)
  * `comfy.rune.kind` (`echo`, `offering`, `custom`)
  * `comfy.rune.payload` (Compact tribute/reward data)
* **Public C# Event Seam**:
  ```csharp
  public static class TotemSentinelApi {
      public static event Action<TotemInteractionContext> OnTotemInteracted;
  }
  ```
* When **Creator OS / ComfyQuestRuntime** is loaded, it intercepts this seam to run full questlines and cinematic cameras. When absent, the mod executes its self-contained local loop.
