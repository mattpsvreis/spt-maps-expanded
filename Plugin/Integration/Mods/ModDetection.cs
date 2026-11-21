using BepInEx.Bootstrap;

namespace MapsExtended.Integration.Mods
{
    /// <summary>
    /// Soft-dependency flags for optional mods that change marker or UI behaviour.
    /// </summary>
    public static class ModDetection
    {
        private const string HeliCrashGuid = "com.SamSWAT.HeliCrash.ArysReloaded";
        private const string FikaGuid = "com.fika.core";
        private const string FikaHeadlessGuid = "com.fika.headless";

        public static bool HeliCrashLoaded { get; private set; }
        public static bool FikaLoaded { get; private set; }
        public static bool FikaHeadlessLoaded { get; private set; }

        public static void CheckforMods()
        {
            HeliCrashLoaded = Loaded(HeliCrashGuid);
            FikaLoaded = Loaded(FikaGuid);
            FikaHeadlessLoaded = Loaded(FikaHeadlessGuid);
        }

        private static bool Loaded(string guid)
        {
            return Chainloader.PluginInfos.ContainsKey(guid);
        }
    }
}
