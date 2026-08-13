using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ComfySentinel.Services
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

            internal int Fulings => Goblins + Shamans + Brutes;

            internal ScanResult(int goblins, int shamans, int brutes, int coins, int blackMetal)
            {
                Goblins = goblins;
                Shamans = shamans;
                Brutes = brutes;
                Coins = coins;
                BlackMetal = blackMetal;
            }
        }

        private static readonly int GoblinPrefabHash = "Goblin".GetStableHashCode();
        private static readonly int GoblinShamanPrefabHash = "GoblinShaman".GetStableHashCode();
        private static readonly int GoblinBrutePrefabHash = "GoblinBrute".GetStableHashCode();
        private static readonly int CoinsPrefabHash = "Coins".GetStableHashCode();
        private static readonly int BlackMetalScrapPrefabHash = "BlackMetalScrap".GetStableHashCode();

        private static AccessTools.FieldRef<ZDOMan, List<ZDO>[]> _objectsBySectorRef;
        private static AccessTools.FieldRef<ZDOMan, int> _widthRef;
        private static AccessTools.FieldRef<ZDOMan, int> _halfWidthRef;
        private static bool _initialized;

        internal static bool Initialize()
        {
            try
            {
                _objectsBySectorRef = AccessTools.FieldRefAccess<ZDOMan, List<ZDO>[]>("m_objectsBySector");
                _widthRef = AccessTools.FieldRefAccess<ZDOMan, int>("m_width");
                _halfWidthRef = AccessTools.FieldRefAccess<ZDOMan, int>("m_halfWidth");
                _initialized = true;
                return true;
            }
            catch (Exception exception)
            {
                _initialized = false;
                ZLog.LogError($"[{ComfySentinelPlugin.PluginName}] Could not bind ZDOMan sector fields: {exception}");
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
            int halfWidth = _halfWidthRef(zdoMan);
            if (objectsBySector == null || width <= 0 || objectsBySector.Length != width * width)
            {
                return false;
            }

            Vector2i centerSector = ZoneSystem.GetZone(centerPoint);
            float radiusSquared = radius * radius;
            int goblins = 0;
            int shamans = 0;
            int brutes = 0;
            int coins = 0;
            int blackMetal = 0;

            for (int sectorY = centerSector.y - 2; sectorY <= centerSector.y + 2; sectorY++)
            {
                int arrayY = sectorY + halfWidth;
                if ((uint)arrayY >= (uint)width)
                {
                    continue;
                }

                int rowStart = arrayY * width;
                for (int sectorX = centerSector.x - 2; sectorX <= centerSector.x + 2; sectorX++)
                {
                    int arrayX = sectorX + halfWidth;
                    if ((uint)arrayX >= (uint)width)
                    {
                        continue;
                    }

                    List<ZDO> sectorObjects = objectsBySector[rowStart + arrayX];
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
                            && prefabHash != BlackMetalScrapPrefabHash)
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

            result = new ScanResult(goblins, shamans, brutes, coins, blackMetal);
            return true;
        }
    }
}
