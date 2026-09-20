using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TotemSentinel.Services
{
    internal static class SonarScanner
    {
        internal readonly struct ScanResult
        {
            internal readonly int Goblins;
            internal readonly int Shamans;
            internal readonly int Brutes;
            internal readonly int Coins;
            internal readonly int BlackMetal;
            internal readonly int Valuables;

            internal int Fulings => Goblins + Shamans + Brutes;

            internal ScanResult(int goblins, int shamans, int brutes, int coins, int blackMetal, int valuables = 0)
            {
                Goblins = goblins;
                Shamans = shamans;
                Brutes = brutes;
                Coins = coins;
                BlackMetal = blackMetal;
                Valuables = valuables;
            }
        }

        private static readonly int GoblinPrefabHash = "Goblin".GetStableHashCode();
        private static readonly int GoblinShamanPrefabHash = "GoblinShaman".GetStableHashCode();
        private static readonly int GoblinBrutePrefabHash = "GoblinBrute".GetStableHashCode();
        private static readonly int CoinsPrefabHash = "Coins".GetStableHashCode();
        private static readonly int BlackMetalScrapPrefabHash = "BlackMetalScrap".GetStableHashCode();
        private static readonly int RubyPrefabHash = "Ruby".GetStableHashCode();
        private static readonly int AmberPrefabHash = "Amber".GetStableHashCode();
        private static readonly int AmberPearlPrefabHash = "AmberPearl".GetStableHashCode();
        private static readonly int SilverNecklacePrefabHash = "SilverNecklace".GetStableHashCode();
        private static readonly int GoblinTotemPrefabHash = "GoblinTotem".GetStableHashCode();

        private static AccessTools.FieldRef<ZDOMan, List<ZDO>[]> _objectsBySectorRef;
        private static AccessTools.FieldRef<ZDOMan, int> _widthRef;
        private static bool _initialized;

        internal static bool Initialize()
        {
            try
            {
                _objectsBySectorRef = AccessTools.FieldRefAccess<ZDOMan, List<ZDO>[]>("m_objectsBySector");
                _widthRef = AccessTools.FieldRefAccess<ZDOMan, int>("m_width");
                _initialized = true;
                return true;
            }
            catch (Exception exception)
            {
                _initialized = false;
                ZLog.LogError($"[{TotemSentinelPlugin.PluginName}] Could not bind ZDOMan sector fields: {exception}");
                return false;
            }
        }

        internal static bool TryScan(Vector3 centerPoint, float radius, out ScanResult result)
        {
            result = default;

            ZDOMan zdoMan = ZDOMan.instance;
            if (!_initialized || zdoMan == null || ZoneSystem.instance == null)
            {
                return false;
            }

            List<ZDO>[] objectsBySector = _objectsBySectorRef(zdoMan);
            int width = _widthRef(zdoMan);
            if (objectsBySector == null || width <= 0 || objectsBySector.Length != width * width)
            {
                return false;
            }

            Vector2s centerSector = ZoneSystem.GetZone(centerPoint);
            float radiusSquared = radius * radius;
            int goblins = 0;
            int shamans = 0;
            int brutes = 0;
            int coins = 0;
            int blackMetal = 0;
            int valuables = 0;

            int sectorRadius = Mathf.Clamp(Mathf.CeilToInt(radius / 64.0f) + 1, 2, 5);

            for (int sectorY = centerSector.y - sectorRadius; sectorY <= centerSector.y + sectorRadius; sectorY++)
            {
                int halfWidth = width / 2;
                if (sectorY < -halfWidth || sectorY >= halfWidth)
                {
                    continue;
                }

                for (int sectorX = centerSector.x - sectorRadius; sectorX <= centerSector.x + sectorRadius; sectorX++)
                {
                    if (sectorX < -halfWidth || sectorX >= halfWidth)
                    {
                        continue;
                    }

                    uint sectorIndex = ZoneSystem.SectorToIndex(sectorX, sectorY).Sector;
                    if (sectorIndex >= objectsBySector.Length)
                    {
                        continue;
                    }

                    List<ZDO> sectorObjects = objectsBySector[sectorIndex];
                    if (sectorObjects == null)
                    {
                        continue;
                    }

                    for (int objectIndex = 0; objectIndex < sectorObjects.Count; objectIndex++)
                    {
                        ZDO zdo = sectorObjects[objectIndex];
                        if (zdo == null)
                        {
                            continue;
                        }

                        int prefabHash = zdo.GetPrefab();
                        if (prefabHash != GoblinPrefabHash
                            && prefabHash != GoblinShamanPrefabHash
                            && prefabHash != GoblinBrutePrefabHash
                            && prefabHash != CoinsPrefabHash
                            && prefabHash != BlackMetalScrapPrefabHash
                            && prefabHash != RubyPrefabHash
                            && prefabHash != AmberPrefabHash
                            && prefabHash != AmberPearlPrefabHash
                            && prefabHash != SilverNecklacePrefabHash
                            && prefabHash != GoblinTotemPrefabHash)
                        {
                            continue;
                        }

                        if ((zdo.GetPosition() - centerPoint).sqrMagnitude > radiusSquared)
                        {
                            continue;
                        }

                        if (prefabHash == CoinsPrefabHash)
                        {
                            int stack = zdo.GetInt(ZDOVars.s_stack, 1);
                            coins += stack > 0 ? stack : 1;
                        }
                        else if (prefabHash == BlackMetalScrapPrefabHash)
                        {
                            int stack = zdo.GetInt(ZDOVars.s_stack, 1);
                            blackMetal += stack > 0 ? stack : 1;
                        }
                        else if (prefabHash == RubyPrefabHash
                            || prefabHash == AmberPrefabHash
                            || prefabHash == AmberPearlPrefabHash
                            || prefabHash == SilverNecklacePrefabHash
                            || prefabHash == GoblinTotemPrefabHash)
                        {
                            int stack = zdo.GetInt(ZDOVars.s_stack, 1);
                            valuables += stack > 0 ? stack : 1;
                        }
                        else if (prefabHash == GoblinPrefabHash)
                        {
                            goblins++;
                        }
                        else if (prefabHash == GoblinShamanPrefabHash)
                        {
                            shamans++;
                        }
                        else
                        {
                            brutes++;
                        }
                    }
                }
            }

            result = new ScanResult(goblins, shamans, brutes, coins, blackMetal, valuables);
            return true;
        }
    }
}

