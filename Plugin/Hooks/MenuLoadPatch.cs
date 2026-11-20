using MapsExtended.Core;
using MapsExtended.Presentation;
using Newtonsoft.Json;
using SPT.Common.Http;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using System;
using System.Reflection;
using System.Threading.Tasks;
using EFT.Communications;

namespace MapsExtended.Hooks
{
    internal class MenuLoadPatch : ModulePatch
    {
        private static bool _serverConfigLoaded;
        private static bool _loadInFlight;

        protected override MethodBase GetTargetMethod()
        {
            return PatchConstants.EftTypes
                .SingleCustom(x => x.GetField("Taxonomy", BindingFlags.Public | BindingFlags.Instance) != null)
                .GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        }

        [PatchPostfix]
        public static async void PatchPostfix()
        {
            if (_serverConfigLoaded || _loadInFlight)
            {
                return;
            }

            _loadInFlight = true;
            try
            {
                var config = await LoadFromServer();
                if (config == null)
                {
                    // Leave unloaded so a later menu create can retry.
                    return;
                }

                RaidMapHost.ServerConfig = config;
                _serverConfigLoaded = true;
                Plugin.Log?.LogInfo("Loaded Maps Extended server config");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogError("Caught error while trying to load Maps Extended server config");
                Plugin.Log?.LogError(e.ToString());
            }
            finally
            {
                _loadInFlight = false;
            }
        }

        private static async Task<ServerMapPolicy> LoadFromServer()
        {
            try
            {
                var payload = await RequestHandler.GetJsonAsync("/mapsextended/load");
                var config = JsonConvert.DeserializeObject<ServerMapPolicy>(payload);
                return config ?? throw new InvalidOperationException("Server config was empty");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError("Failed to load Maps Extended server config: " + ex);
                NotificationManager.DisplayWarningNotification("Failed to load Maps Extended server config - check the server");
                return null;
            }
        }
    }
}
