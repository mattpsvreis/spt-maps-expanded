using System;
using System.Collections.Generic;
using System.Reflection;
using MapsExtended.App;
using MapsExtended.Integration;
using SPT.Reflection.Patching;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;

namespace MapsExtended.Hooks
{
    internal class PlayerOnDeadPatch : ModulePatch
    {
        internal static event Action<Player> OnDead
        {
            add => RaidEvents.PlayerDied += value;
            remove => RaidEvents.PlayerDied -= value;
        }

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), nameof(Player.OnDead));
        }

        [PatchPostfix]
        public static void PatchPostfix(Player __instance)
        {
            RaidEvents.RaisePlayerDied(__instance);
        }
    }

    internal class PlayerInventoryThrowItemPatch : ModulePatch
    {
        internal static event Action<int, Item> OnThrowItem;
        internal static Dictionary<int, Item> ThrownItems = [];
        private bool _hooked;

        protected override MethodBase GetTargetMethod()
        {
            if (!_hooked)
            {
                RaidEvents.RaidEnded += () => ThrownItems.Clear();
                RaidEvents.LootDestroyed += OnDestroyLoot;
                _hooked = true;
            }

            return AccessTools.Method(typeof(Player.PlayerInventoryController),
                nameof(Player.PlayerInventoryController.ThrowItem));
        }

        [PatchPostfix]
        public static void PatchPostfix(Player.PlayerInventoryController __instance, Item item)
        {
            var player = __instance.Player;
            if (player == null || player != GameUtils.GetMainPlayer())
            {
                return;
            }

            var netId = item.Id.GetHashCode();
            ThrownItems[netId] = item;
            OnThrowItem?.Invoke(netId, item);
        }

        private static void OnDestroyLoot(LootItem lootItem)
        {
            if (lootItem?.Item != null)
            {
                ThrownItems.Remove(lootItem.GetNetId());
            }
        }
    }
}
