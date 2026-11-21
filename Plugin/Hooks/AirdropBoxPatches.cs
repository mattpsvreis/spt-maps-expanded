using System;
using System.Collections.Generic;
using System.Reflection;
using MapsExtended.App;
using EFT.Airdrop;
using EFT.SynchronizableObjects;
using SPT.Reflection.Patching;
using HarmonyLib;

namespace MapsExtended.Hooks
{
    internal class AirdropBoxOnBoxLandPatch : ModulePatch
    {
        internal static event Action<AirdropSynchronizableObject> OnAirdropLanded;
        internal static List<AirdropSynchronizableObject> Airdrops = [];
        private bool _hooked;

        protected override MethodBase GetTargetMethod()
        {
            if (!_hooked)
            {
                RaidEvents.RaidEnded += () => Airdrops.Clear();
                _hooked = true;
            }

            return AccessTools.Method(typeof(ClientAirDrop), nameof(ClientAirDrop.CloseParachute));
        }

        [PatchPostfix]
        public static void PatchPostfix(ClientAirDrop __instance)
        {
            if (__instance == null || Airdrops.Contains(__instance._syncObject))
            {
                return;
            }

            Airdrops.Add(__instance._syncObject);
            OnAirdropLanded?.Invoke(__instance._syncObject);
        }
    }
}
