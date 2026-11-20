using System;
using System.Reflection;
using SPT.Reflection.Patching;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using EFT.UI.Map;
using HarmonyLib;

namespace MapsExtended.Hooks
{
    internal class MapScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MapScreen), nameof(MapScreen.Show));
        }

        [PatchPrefix]
        public static bool PatchPrefix()
        {
            try
            {
                if (!Settings.ReplaceMapScreen.Value || !GameUtils.ShouldShowMapInRaid())
                {
                    // mod is disabled
                    Plugin.Instance.Map?.OnMapScreenClose();
                    return true;
                }

                // show instead
                Plugin.Instance.Map?.OnMapScreenShow();
                return false;
            }
            catch(Exception e)
            {
                Plugin.Log.LogError($"Caught error while trying to show map");
                Plugin.Log.LogError($"{e.Message}");
                Plugin.Log.LogError($"{e.StackTrace}");

                // Keep the vanilla map closed. Falling through draws it behind the inventory tabs.
                return !Settings.ReplaceMapScreen.Value;
            }
        }
    }

    internal class MapScreenClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MapScreen), nameof(MapScreen.Close));
        }

        [PatchPrefix]
        public static bool PatchPrefix()
        {
            try
            {
                if (!Settings.ReplaceMapScreen.Value || !GameUtils.ShouldShowMapInRaid())
                {
                    // mod is disabled
                    return true;
                }

                // close instead
                Plugin.Instance.Map?.OnMapScreenClose();
                return false;
            }
            catch(Exception e)
            {
                Plugin.Log.LogError($"Caught error while trying to close map");
                Plugin.Log.LogError($"{e.Message}");
                Plugin.Log.LogError($"{e.StackTrace}");

                return true;
            }
        }
    }
}
