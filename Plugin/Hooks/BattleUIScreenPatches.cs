using System;
using System.Reflection;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using SPT.Reflection.Patching;
using EFT;
using EFT.UI;
using HarmonyLib;

namespace MapsExtended.Hooks
{
    internal class BattleUIScreenShowPatch : ModulePatch
    {
        public static bool IsAttached = false;
        
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EftBattleUIScreen),
                                      nameof(EftBattleUIScreen.Show),
                                      new Type[] { typeof(GamePlayerOwner) });
        }

        [PatchPostfix]
        public static void PatchPostfix(EftBattleUIScreen __instance)
        {
            IsAttached = GameUtils.ShouldShowMapInRaid();
            if (!IsAttached) return;
            
            Plugin.Instance.TryAttachToBattleUIScreen(__instance);
        }
    }
}
