using BepInEx.Bootstrap;

namespace MapsExtended.Integration
{
    internal static class PluginDetection
    {
        internal static bool Loaded(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            var infos = Chainloader.PluginInfos;
            if (infos == null)
            {
                return false;
            }

            return infos.ContainsKey(guid);
        }
    }
}
