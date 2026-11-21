using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MapsExtended.App;
using SPT.Reflection.Patching;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using Object = UnityEngine.Object;

namespace MapsExtended.Hooks
{
    internal class GameStartedPatch : ModulePatch
    {
        public static List<LootableContainer> HiddenStashes { get; } = [];

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
        }

        [PatchPostfix]
        public static void PatchPostfix(GameWorld __instance)
        {
            foreach (var cache in LocationScene.GetAllObjects<LootableContainer>()
                         .Where(x => x.name.StartsWith("scontainer_wood_CAP")
                                     || x.name.StartsWith("scontainer_Blue_Barrel_Base_Cap")))
            {
                HiddenStashes.Add(cache);
            }
        }
    }

    internal class GameWorldOnDestroyPatch : ModulePatch
    {
        // Kept for existing subscribers while RaidEvents becomes the main path.
        internal static event Action OnRaidEnd
        {
            add => RaidEvents.RaidEnded += value;
            remove => RaidEvents.RaidEnded -= value;
        }

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnDestroy));
        }

        [PatchPrefix]
        public static void PatchPrefix()
        {
            RaidEvents.RaiseRaidEnded();
            GameStartedPatch.HiddenStashes.Clear();
        }
    }

    internal class GameWorldUnregisterPlayerPatch : ModulePatch
    {
        internal static event Action<IPlayer> OnUnregisterPlayer
        {
            add => RaidEvents.PlayerUnregistered += value;
            remove => RaidEvents.PlayerUnregistered -= value;
        }

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.UnregisterPlayer));
        }

        [PatchPostfix]
        public static void PatchPostfix(IPlayer iPlayer)
        {
            RaidEvents.RaisePlayerUnregistered(iPlayer);
        }
    }

    internal class LootItemInitPatch : ModulePatch
    {
        internal static event Action<LootItem> OnRegisterLoot
        {
            add => RaidEvents.LootRegistered += value;
            remove => RaidEvents.LootRegistered -= value;
        }

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(LootItem), nameof(LootItem.Init));
        }

        [PatchPostfix]
        public static void PatchPostfix(LootItem __instance, Item item)
        {
            if (item != null)
            {
                RaidEvents.RaiseLootRegistered(__instance);
            }
        }
    }

    internal class GameWorldDestroyLootPatch : ModulePatch
    {
        internal static event Action<LootItem> OnDestroyLoot
        {
            add => RaidEvents.LootDestroyed += value;
            remove => RaidEvents.LootDestroyed -= value;
        }

        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == "DestroyLoot" && m.GetParameters().FirstOrDefault(p => p.Name == "loot") != null);
        }

        [PatchPrefix]
        public static void PatchPrefix(Object loot)
        {
            try
            {
                if (loot is LootItem lootItem)
                {
                    RaidEvents.RaiseLootDestroyed(lootItem);
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogError($"Caught error while running DestroyLoot patch: {e}");
            }
        }
    }
}
