using System;
using System.Reflection;
using MapsExtended.Config;
using SPT.Reflection.Patching;
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
                if (!Settings.ReplaceMapScreen.Value)
                {
                    Plugin.Instance.Map?.OnMapScreenClose();
                    return true;
                }

                Plugin.Instance.Map?.OnMapScreenShow();
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log?.LogError($"Caught error while trying to show map: {e}");
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
                if (!Settings.ReplaceMapScreen.Value)
                {
                    return true;
                }

                Plugin.Instance.Map?.OnMapScreenClose();
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log?.LogError($"Caught error while trying to close map: {e}");
                return true;
            }
        }
    }
}
