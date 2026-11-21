using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using DrakiaXYZ.VersionChecker;
using MapsExtended.Config;
using MapsExtended.Integration;
using MapsExtended.Hooks;
using MapsExtended.Presentation;
using MapsExtended.Core;
using EFT;
using EFT.UI;
using EFT.UI.Map;
using SPT.Custom.Utils;
using System;
using System.Diagnostics;
using System.Reflection;

namespace MapsExtended
{
    [BepInPlugin(Guid, Name, MapsExtended.BuildInfo.Version)]
    [BepInDependency("com.SPT.custom", "4.1.0")]
    [BepInDependency("com.SamSWAT.HeliCrash.ArysReloaded", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(MapAlias.MapVariantsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(MapAlias.FactoryClassicGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(MapAlias.ManimalInterchangeGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(MapAlias.ManimalLabsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.manimal.icebreaker", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.fika.headless", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("com.mpstark.dynamicmaps")]
    [BepInIncompatibility("com.blackhawk.dynamicmapsextended")]
    [BepInIncompatibility("com.dark.dynamicmapsplus")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.dark.mapsexpanded";
        public const string Name = "Maps Expanded";
        public const int TarkovVersion = 40743;

        public static Plugin Instance;
        public static ManualLogSource Log => Instance != null ? Instance.Logger : null;
        public static readonly string Path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        internal static ConfigEntry<bool> DebugLogging { get; private set; }
        internal static bool DebugEnabled => DebugLogging != null && DebugLogging.Value;

        internal static void DebugLog(string message)
        {
            if (DebugEnabled)
            {
                Log?.LogInfo("[MapsExpanded:DBG] " + message);
            }
        }

        public RaidMapHost Map;
        private bool _disabledForHeadless;

        internal void Awake()
        {
            Instance = this;
            Integration.Mods.ModDetection.CheckforMods();

            if (Integration.Mods.ModDetection.FikaHeadlessLoaded)
            {
                _disabledForHeadless = true;
                Logger.LogInfo($"{Name}: Fika Headless detected — client/UI features disabled.");
                enabled = false;
                return;
            }

            if (!VersionChecker.CheckEftVersion(Logger, Info, Config))
            {
                throw new Exception("Invalid EFT Version");
            }

            DebugLogging = Config.Bind(
                "0. Troubleshooting",
                "Write extra log notes",
                false,
                "Add Maps Expanded notes to the BepInEx log, covering faction colours and which map pack was chosen.");

            Settings.Init(Config);
            FactionColours.Initialize(Config);
            VariantRefresh.Initialize();
            Config.SettingChanged += (x, y) => Map?.ReadConfig();

            // patches
            new BattleUIScreenShowPatch().Enable();
            new CommonUIAwakePatch().Enable();
            new MapScreenShowPatch().Enable();
            new MapScreenClosePatch().Enable();
            new GameStartedPatch().Enable();
            new GameWorldOnDestroyPatch().Enable();
            new GameWorldUnregisterPlayerPatch().Enable();
            new LootItemInitPatch().Enable();
            new GameWorldDestroyLootPatch().Enable();
            new AirdropBoxOnBoxLandPatch().Enable();
            new PlayerOnDeadPatch().Enable();
            new PlayerInventoryThrowItemPatch().Enable();
            new ShowViewButtonPatch().Enable();
            new MenuLoadPatch().Enable();

            Logger.LogInfo($"{Name} {BuildInfo.Version} loaded");
            Logger.LogInfo($"MapVariants={PluginDetection.Loaded(MapAlias.MapVariantsGuid)}, FactoryClassic={PluginDetection.Loaded(MapAlias.FactoryClassicGuid)}, Interchange={PluginDetection.Loaded(MapAlias.ManimalInterchangeGuid)}, Labs={PluginDetection.Loaded(MapAlias.ManimalLabsGuid)}");
        }

        private void OnDestroy()
        {
            if (_disabledForHeadless)
            {
                Instance = null;
                return;
            }

            VariantRefresh.Shutdown();
            FactionColours.Shutdown();
            Instance = null;
        }

        internal void TryAttachToMapScreen(MapScreen mapScreen)
        {
            if (Map is not null) return;

            Log.LogInfo("Trying to attach to MapScreen");
            Map = RaidMapHost.Create(Singleton<CommonUI>.Instance.gameObject);
            Map.transform.SetParent(mapScreen.transform);
        }

        internal void TryAttachToBattleUIScreen(EftBattleUIScreen battleUI)
        {
            if (Map is null || GameUtils.GetMainPlayer() is HideoutPlayer)
            {
                return;
            }

            Map.TryAddPeekComponent(battleUI);
        }

        static Plugin()
        {
            if (Type.GetType("Unity.VectorGraphics.SVGParser, Unity.VectorGraphics") == null)
            {
                MessageBoxHelper.Show(
                    "You have incorrectly installed Maps Expanded.\nUnity.VectorGraphics is missing.\nPlease reinstall correctly.\nQuitting.",
                    "Maps Expanded Error",
                    MessageBoxHelper.MessageBoxType.OK);
                Process.GetCurrentProcess().Kill();
            }
        }
    }
}
