using System;
using System.Reflection;
using System.Threading.Tasks;
using MapsExtended.Core;
using MapsExtended.Presentation;
using Newtonsoft.Json;
using SPT.Common.Http;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using EFT.Communications;

namespace MapsExtended.Hooks
{
    internal class MenuLoadPatch : ModulePatch
    {
        private static bool _loaded;
        private static bool _busy;

        protected override MethodBase GetTargetMethod()
        {
            return PatchConstants.EftTypes
                .SingleCustom(x => x.GetField("Taxonomy", BindingFlags.Public | BindingFlags.Instance) != null)
                .GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        }

        [PatchPostfix]
        public static async void PatchPostfix()
        {
            if (_loaded || _busy)
            {
                return;
            }

            _busy = true;
            try
            {
                var policy = await FetchPolicy();
                if (policy == null)
                {
                    return;
                }

                RaidMapHost.ServerConfig = policy;
                _loaded = true;
                Plugin.Log?.LogInfo("Loaded Maps Expanded server config");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogError("Caught error while trying to load Maps Expanded server config");
                Plugin.Log?.LogError(e.ToString());
            }
            finally
            {
                _busy = false;
            }
        }

        private static async Task<ServerMapPolicy> FetchPolicy()
        {
            try
            {
                var payload = await RequestHandler.GetJsonAsync("/mapsexpanded/load");
                return JsonConvert.DeserializeObject<ServerMapPolicy>(payload)
                       ?? throw new InvalidOperationException("Server config was empty");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("Failed to load Maps Expanded server config: " + ex);
                NotificationManager.DisplayWarningNotification("Failed to load Maps Expanded server config - check the server");
                return null;
            }
        }
    }
}
