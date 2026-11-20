using BepInEx.Bootstrap;

namespace MapsExtended.Features
{
    internal static class PluginDetection
    {
        internal static bool Loaded(string guid)
            => !string.IsNullOrEmpty(guid)
               && Chainloader.PluginInfos != null
               && Chainloader.PluginInfos.ContainsKey(guid);
    }
}
