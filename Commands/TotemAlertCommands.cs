using System;
using System.Collections.Generic;
using UnityEngine;

namespace ComfySentinel.Commands
{
    internal static class TotemAlertCommands
    {
        private static readonly List<GameObject> SpawnedTestObjects = new List<GameObject>(7);
        private static bool _registered;

        internal static void Register()
        {
            if (_registered)
            {
                return;
            }

            new Terminal.ConsoleCommand(
                "totemalert",
                "[on/off/status] - preview the ComfySentinel camp-check panel",
                RunTotemAlertCommand);

            new Terminal.ConsoleCommand(
                "totem",
                "1 - spawn the ComfySentinel test camp; 'totem clear' removes it",
                RunTotemCommand,
                isCheat: false,
                isNetwork: true);

            _registered = true;
        }

        private static object RunTotemAlertCommand(Terminal.ConsoleEventArgs args)
        {
            bool visible;
            if (args.Length == 1)
            {
                visible = ComfySentinelPlugin.SetAbilityPreview();
            }
            else if (string.Equals(args[1], "on", StringComparison.OrdinalIgnoreCase))
            {
                visible = ComfySentinelPlugin.SetAbilityPreview(true);
            }
            else if (string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase))
            {
                visible = ComfySentinelPlugin.SetAbilityPreview(false);
            }
            else if (string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
            {
                WriteStatus(args.Context);
                return true;
            }
            else
            {
                return "Use: totemalert [on|off|status]";
            }

            args.Context?.AddString(
                $"ComfySentinel {ComfySentinelPlugin.PluginVersion} is running. Panel preview: {(visible ? "open" : "closed")}.");
            args.Context?.AddString("Use 'totem 1' to create the test camp and 'totem clear' to remove it.");
            return true;
        }

        private static object RunTotemCommand(Terminal.ConsoleEventArgs args)
        {
            if (args.Length < 2)
            {
                return "Use: totem 1 | totem clear";
            }

            if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
            {
                int removed = ClearTestScenario();
                args.Context?.AddString($"ComfySentinel test camp cleared ({removed} tracked objects removed)." );
                return true;
            }

            if (args[1] != "1")
            {
                return "Unknown scenario. Use: totem 1 | totem clear";
            }

            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return "Test spawning is restricted to a local world or a world hosted by this client.";
            }

            Player player = Player.m_localPlayer;
            ZNetScene scene = ZNetScene.instance;
            if (player == null || scene == null || ZoneSystem.instance == null)
            {
                return "Enter a fully loaded local/hosted world before spawning the test camp.";
            }

            try
            {
                ClearTestScenario();
                string error = SpawnScenarioOne(player, scene);
                if (error != null)
                {
                    ClearTestScenario();
                    return error;
                }

                args.Context?.AddString("ComfySentinel scenario 1 created: totem, three Fuling types, 12 coins, and 8 black metal.");
                args.Context?.AddString(
                    $"Close the console, pick up the loose totem to add " +
                    $"{ComfySentinelPlugin.MaxSonarCharges.Value} checks, then press the configured hotkey when ready.");
                ZLog.Log($"[{ComfySentinelPlugin.PluginName}] Test scenario 1 created with 7 tracked objects.");
                return true;
            }
            catch (Exception exception)
            {
                ClearTestScenario();
                ZLog.LogError($"[{ComfySentinelPlugin.PluginName}] Failed to create test scenario 1: {exception}");
                return $"Scenario creation failed: {exception.Message}";
            }
        }

        private static string SpawnScenarioOne(Player player, ZNetScene scene)
        {
            GameObject standPrefab = FindPrefab(scene, "itemstand", "itemstandh", "piece_itemstand");
            GameObject totemPrefab = FindPrefab(scene, "GoblinTotem");
            GameObject goblinPrefab = FindPrefab(scene, "Goblin");
            GameObject shamanPrefab = FindPrefab(scene, "GoblinShaman");
            GameObject brutePrefab = FindPrefab(scene, "GoblinBrute");
            GameObject coinsPrefab = FindPrefab(scene, "Coins");
            GameObject blackMetalPrefab = FindPrefab(scene, "BlackMetalScrap");
            GameObject pickableTemplate = FindPickableTemplate(scene);

            if (standPrefab == null)
            {
                return "Could not find an item stand prefab in this Valheim build.";
            }

            if (totemPrefab == null
                || goblinPrefab == null
                || shamanPrefab == null
                || brutePrefab == null
                || coinsPrefab == null
                || blackMetalPrefab == null)
            {
                return "One or more required Fuling or loose-loot prefabs are unavailable in this world.";
            }

            if (pickableTemplate == null)
            {
                return "Could not find a native Pickable prefab to use as the test interaction proxy.";
            }

            Vector3 forward = player.transform.forward;
            forward.y = 0.0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 standPosition = GroundPosition(player.transform.position + forward * 5.0f);
            Quaternion standRotation = Quaternion.LookRotation(-forward, Vector3.up);

            GameObject standObject = Spawn(standPrefab, standPosition, standRotation);
            ItemStand itemStand = standObject.GetComponent<ItemStand>();
            ZNetView standNetView = standObject.GetComponent<ZNetView>();
            if (itemStand == null || standNetView == null || !standNetView.IsValid())
            {
                return "The available item stand prefab is missing its expected Valheim components.";
            }

            ConfigureStandVisual(standNetView, totemPrefab.name);

            Vector3 pickablePosition = itemStand.m_attachOther != null
                ? itemStand.m_attachOther.position
                : standPosition + Vector3.up * 1.25f;

            GameObject pickableObject = Spawn(pickableTemplate, pickablePosition, standRotation);
            ConfigurePickableTotem(pickableObject, totemPrefab);

            Spawn(goblinPrefab, GroundPosition(standPosition + forward * 13.0f + right * 9.0f) + Vector3.up, Quaternion.identity);
            Spawn(shamanPrefab, GroundPosition(standPosition + forward * 17.0f - right * 11.0f) + Vector3.up, Quaternion.identity);
            Spawn(brutePrefab, GroundPosition(standPosition + forward * 23.0f + right * 2.0f) + Vector3.up, Quaternion.identity);
            SpawnItemStack(coinsPrefab, 12, GroundPosition(standPosition + forward * 9.0f - right * 4.0f) + Vector3.up);
            SpawnItemStack(blackMetalPrefab, 8, GroundPosition(standPosition + forward * 11.0f + right * 4.0f) + Vector3.up);
            return null;
        }

        private static void SpawnItemStack(GameObject prefab, int stack, Vector3 position)
        {
            GameObject itemObject = Spawn(prefab, position, Quaternion.identity);
            ItemDrop itemDrop = itemObject.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                throw new InvalidOperationException($"Test loot prefab {prefab.name} is not an ItemDrop.");
            }

            itemDrop.SetStack(stack);
        }

        private static void ConfigureStandVisual(ZNetView standNetView, string itemPrefabName)
        {
            ZDO zdo = standNetView.GetZDO();
            zdo.Set(ZDOVars.s_item, itemPrefabName);
            zdo.Set(ZDOVars.s_type, 0);
            standNetView.InvokeRPC(
                ZNetView.Everybody,
                "SetVisualItem",
                itemPrefabName,
                0,
                1,
                0);
        }

        private static void ConfigurePickableTotem(GameObject pickableObject, GameObject totemPrefab)
        {
            Pickable pickable = pickableObject.GetComponent<Pickable>();
            if (pickable == null)
            {
                throw new InvalidOperationException("The selected Pickable template did not instantiate correctly.");
            }

            pickable.CancelInvoke();
            pickable.m_itemPrefab = totemPrefab;
            pickable.m_amount = 1;
            pickable.m_minAmountScaled = 1;
            pickable.m_dontScale = true;
            pickable.m_extraDrops = new DropTable();
            pickable.m_hideWhenPicked = null;
            pickable.m_respawnTimeMinutes = 0.0f;
            pickable.m_tarPreventsPicking = false;
            pickable.m_aggravateRange = 0.0f;
            pickable.m_harvestable = false;
            pickable.m_pickRaiseSkill = Skills.SkillType.None;

            Renderer[] renderers = pickableObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = false;
            }

            SphereCollider interactionCollider = pickableObject.AddComponent<SphereCollider>();
            interactionCollider.radius = 0.6f;
            interactionCollider.isTrigger = false;
        }

        private static GameObject FindPickableTemplate(ZNetScene scene)
        {
            GameObject preferred = FindPrefab(scene, "Pickable_Branch", "pickable_branch", "Pickable_Barley", "pickable_barley");
            if (preferred != null && preferred.GetComponent<Pickable>() != null)
            {
                return preferred;
            }

            for (int index = 0; index < scene.m_prefabs.Count; index++)
            {
                GameObject prefab = scene.m_prefabs[index];
                if (prefab != null && prefab.GetComponent<Pickable>() != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        private static GameObject FindPrefab(ZNetScene scene, params string[] candidateNames)
        {
            for (int index = 0; index < candidateNames.Length; index++)
            {
                GameObject directMatch = scene.GetPrefab(candidateNames[index]);
                if (directMatch != null)
                {
                    return directMatch;
                }
            }

            for (int prefabIndex = 0; prefabIndex < scene.m_prefabs.Count; prefabIndex++)
            {
                GameObject prefab = scene.m_prefabs[prefabIndex];
                if (prefab == null)
                {
                    continue;
                }

                for (int candidateIndex = 0; candidateIndex < candidateNames.Length; candidateIndex++)
                {
                    if (string.Equals(prefab.name, candidateNames[candidateIndex], StringComparison.OrdinalIgnoreCase))
                    {
                        return prefab;
                    }
                }
            }

            return null;
        }

        private static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject spawned = UnityEngine.Object.Instantiate(prefab, position, rotation);
            ZNetView netView = spawned.GetComponent<ZNetView>();
            if (netView != null && netView.IsValid())
            {
                netView.GetZDO().Persistent = false;
            }

            SpawnedTestObjects.Add(spawned);
            return spawned;
        }

        private static Vector3 GroundPosition(Vector3 position)
        {
            if (ZoneSystem.instance.GetGroundHeight(position, out float height))
            {
                position.y = height;
            }

            return position;
        }

        internal static int ClearTestScenario()
        {
            int removed = 0;
            ZNetScene scene = ZNetScene.instance;

            for (int index = SpawnedTestObjects.Count - 1; index >= 0; index--)
            {
                GameObject spawned = SpawnedTestObjects[index];
                if (spawned == null)
                {
                    continue;
                }

                if (scene != null)
                {
                    scene.Destroy(spawned);
                }
                else
                {
                    UnityEngine.Object.Destroy(spawned);
                }

                removed++;
            }

            SpawnedTestObjects.Clear();
            if (removed > 0)
            {
                ZLog.Log($"[{ComfySentinelPlugin.PluginName}] Cleared {removed} tracked test scenario objects.");
            }

            return removed;
        }

        private static void WriteStatus(Terminal context)
        {
            context?.AddString($"ComfySentinel {ComfySentinelPlugin.PluginVersion}: running");
            context?.AddString(
                $"Sonar session: {(ComfySentinelPlugin.HasActiveSonarSession ? "active" : "waiting for a totem")}; " +
                $"charges: {ComfySentinelPlugin.ActiveSonarCharges}/{ComfySentinelPlugin.MaximumStoredSonarCharges}; " +
                $"per totem: {ComfySentinelPlugin.MaxSonarCharges.Value}; radius: {ComfySentinelPlugin.ScanRadius.Value:0.#}m; " +
                $"hotkey: {ComfySentinelPlugin.PingHotkey.Value}");
            context?.AddString(
                $"State duration: {ComfySentinelPlugin.StateDuration.Value:0.#}s; " +
                $"final summary: {ComfySentinelPlugin.FinalSummaryDuration.Value:0.#}s");
            context?.AddString($"Tracked scenario objects: {SpawnedTestObjects.Count}");
        }
    }
}
