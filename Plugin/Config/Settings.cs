using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using MapsExtended.Integration.Mods;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using UnityEngine;

// THIS IS HEAVILY BASED ON DRAKIAXYZ'S SPT-QuickMoveToContainer
namespace MapsExtended.Config
{
    internal static class Settings
    {
        private static readonly List<ConfigEntryBase> ConfigEntries = [];
        private static readonly Dictionary<string, ConfigEntry<string>> MapPreferences = new(StringComparer.OrdinalIgnoreCase);
        private static ConfigFile _config;

        #region General

        private const string GeneralTitle = "1. General";
        public static ConfigEntry<bool> ReplaceMapScreen;
        public static ConfigEntry<KeyboardShortcut> CenterOnPlayerHotkey;
        public static ConfigEntry<KeyboardShortcut> DumpInfoHotkey;
        public static ConfigEntry<KeyboardShortcut> MoveMapUpHotkey;
        public static ConfigEntry<KeyboardShortcut> MoveMapDownHotkey;
        public static ConfigEntry<KeyboardShortcut> MoveMapLeftHotkey;
        public static ConfigEntry<KeyboardShortcut> MoveMapRightHotkey;
        public static ConfigEntry<float> MapMoveHotkeySpeed;
        public static ConfigEntry<KeyboardShortcut> ChangeMapLevelUpHotkey;
        public static ConfigEntry<KeyboardShortcut> ChangeMapLevelDownHotkey;
        public static ConfigEntry<bool> ZoomMainMapToMouse;
        public static ConfigEntry<KeyboardShortcut> ZoomMapInHotkey;
        public static ConfigEntry<KeyboardShortcut> ZoomMapOutHotkey;
        public static ConfigEntry<float> ZoomMapHotkeySpeed;

        #endregion
        
        #region Markers

        private const string DynamicMarkerTitle = "2. Markers";
        public static ConfigEntry<string> MarkerPack;
        public static ConfigEntry<bool> ShowPlayerMarker;
        public static ConfigEntry<bool> ShowFriendlyPlayerMarkersInRaid;
        public static ConfigEntry<bool> ShowEnemyPlayerMarkersInRaid;
        public static ConfigEntry<bool> ShowScavMarkersInRaid;
        public static ConfigEntry<bool> ShowBossMarkersInRaid;
        public static ConfigEntry<bool> ShowNonVanillaBotMarkersInRaid;
        public static ConfigEntry<bool> NonVanillaBotUseStar;
        public static ConfigEntry<bool> ShowLockedDoorStatus;
        public static ConfigEntry<bool> ShowQuestsInRaid;
        public static ConfigEntry<bool> ShowExtractsInRaid;
        public static ConfigEntry<bool> ShowExtractStatusInRaid;
        public static ConfigEntry<bool> ShowTransitPointsInRaid;
        public static ConfigEntry<bool> ShowSecretPointsInRaid;
        public static ConfigEntry<bool> ShowDroppedBackpackInRaid;
        public static ConfigEntry<bool> ShowBTRInRaid;
        public static ConfigEntry<bool> ShowAirdropsInRaid;
        public static ConfigEntry<bool> ShowHiddenStashesInRaid;
        public static ConfigEntry<bool> ShowFriendlyCorpsesInRaid;
        public static ConfigEntry<bool> ShowKilledCorpsesInRaid;
        public static ConfigEntry<bool> ShowFriendlyKilledCorpsesInRaid;
        public static ConfigEntry<bool> ShowBossCorpsesInRaid;
        public static ConfigEntry<bool> ShowOtherCorpsesInRaid;
        
        #endregion

        #region In Raid

        private const string InRaidTitle = "3. In-Raid";
        public static ConfigEntry<bool> ResetZoomOnCenter;
        public static ConfigEntry<float> CenteringZoomResetPoint;
        public static ConfigEntry<float> ZoomMainMap;
        public static ConfigEntry<bool> AutoCenterOnPlayerMarker;
        public static ConfigEntry<bool> RetainMapPosition;
        public static ConfigEntry<bool> AutoSelectLevel;
        public static ConfigEntry<KeyboardShortcut> PeekShortcut;
        public static ConfigEntry<bool> HoldForPeek;

        #endregion

        #region Mini Map

        private const string MiniMapTitle = "4. Minimap";
        public static ConfigEntry<bool> MiniMapEnabled;
        public static ConfigEntry<EMiniMapPosition> MiniMapPosition;
        public static ConfigEntry<float> MiniMapSizeX;
        public static ConfigEntry<float> MiniMapSizeY;
        public static ConfigEntry<float> MiniMapScreenOffsetX;
        public static ConfigEntry<float> MiniMapScreenOffsetY;
        public static ConfigEntry<bool> MapTransitionEnabled;
        public static ConfigEntry<KeyboardShortcut> MiniMapShowOrHide;
        public static ConfigEntry<float> ZoomMiniMap;
        public static ConfigEntry<KeyboardShortcut> ZoomInMiniMapHotkey;
        public static ConfigEntry<KeyboardShortcut> ZoomOutMiniMapHotkey;
        
        #endregion

        #region Colors

        private const string MarkerColors = "5. Colours";
        public static ConfigEntry<Color> PlayerColor;
        public static ConfigEntry<Color> FriendlyPlayerColor;
        public static ConfigEntry<Color> PmcBearColor;
        public static ConfigEntry<Color> PmcUsecColor;
        public static ConfigEntry<Color> ScavColor;
        public static ConfigEntry<Color> BossColor;
        public static ConfigEntry<Color> NonVanillaBotColor;
        public static ConfigEntry<Color> AirdropColor;
        public static ConfigEntry<Color> BackpackColor;
        public static ConfigEntry<Color> KilledCorpseColor;
        public static ConfigEntry<Color> KilledBossColor;
        public static ConfigEntry<Color> KilledOtherColor;
        public static ConfigEntry<Color> FriendlyCorpseColor;
        public static ConfigEntry<Color> FriendlyKilledCorpseColor;
        public static ConfigEntry<Color> BtrColor;
        public static ConfigEntry<Color> ExtractDefaultColor;
        public static ConfigEntry<Color> ExtractOpenColor;
        public static ConfigEntry<Color> ExtractClosedColor;
        public static ConfigEntry<Color> ExtractHasRequirementsColor;
        public static ConfigEntry<Color> TransPointColor;
        public static ConfigEntry<Color> SecretPointColor;
        public static ConfigEntry<Color> HiddenStashColor;
        public static ConfigEntry<Color> QuestColor;
        public static ConfigEntry<Color> LockedDoorHasKeyColor;
        public static ConfigEntry<Color> LockedDoorInStashColor;
        public static ConfigEntry<Color> LockedDoorMissingKeyColor;
        public static ConfigEntry<Color> LockedDoorDefaultColor;
        public static ConfigEntry<Color> HeliCrashColor;
        
        #endregion
        
        #region External Mod Support
        
        private const string ExternModSupport = "6. Extra mods";
        public static ConfigEntry<bool> ShowHeliCrashMarker;
        
        
        private const string MapSelectionTitle = "7. Map styles";
        
        #endregion
        
        // public static ConfigEntry<KeyboardShortcut> KeyboardShortcut;

        public static void Init(ConfigFile config)
        {
            _config = config;
            #region General

            ConfigEntries.Add(ReplaceMapScreen = config.Bind(
                GeneralTitle,
                "Use this map in your inventory",
                true,
                new ConfigDescription(
                    "Swap Tarkov's character inventory map for Maps Expanded. After changing this, leave the map tab and open it again.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(CenterOnPlayerHotkey = config.Bind(
                GeneralTitle,
                "Center on you",
                new KeyboardShortcut(KeyCode.Semicolon),
                new ConfigDescription(
                    "While a map is open, jump the view back to your position.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MoveMapUpHotkey = config.Bind(
                GeneralTitle,
                "Pan map up",
                new KeyboardShortcut(KeyCode.UpArrow),
                new ConfigDescription(
                    "Slide the map upward while it is open.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MoveMapDownHotkey = config.Bind(
                GeneralTitle,
                "Pan map down",
                new KeyboardShortcut(KeyCode.DownArrow),
                new ConfigDescription(
                    "Slide the map downward while it is open.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MoveMapLeftHotkey = config.Bind(
                GeneralTitle,
                "Pan map left",
                new KeyboardShortcut(KeyCode.LeftArrow),
                new ConfigDescription(
                    "Slide the map to the left while it is open.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MoveMapRightHotkey = config.Bind(
                GeneralTitle,
                "Pan map right",
                new KeyboardShortcut(KeyCode.RightArrow),
                new ConfigDescription(
                    "Slide the map to the right while it is open.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MapMoveHotkeySpeed = config.Bind(
                GeneralTitle,
                "Pan speed",
                0.25f,
                new ConfigDescription(
                    "How quickly the pan keys move the map. 0.05 is a crawl, 2 crosses the map quickly.",
                    new AcceptableValueRange<float>(0.05f, 2f),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ChangeMapLevelUpHotkey = config.Bind(
                GeneralTitle,
                "Floor up",
                new KeyboardShortcut(KeyCode.Period),
                new ConfigDescription(
                    "Show the next floor up. Shift and scroll up does the same on the map screen.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ChangeMapLevelDownHotkey = config.Bind(
                GeneralTitle,
                "Floor down",
                new KeyboardShortcut(KeyCode.Comma),
                new ConfigDescription(
                    "Show the next floor down. Shift and scroll down does the same on the map screen.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ZoomMainMapToMouse = config.Bind(
                GeneralTitle,
                "Zoom toward the cursor",
                true,
                new ConfigDescription(
                    "On the inventory map, zoom keys and the scroll wheel zoom toward the cursor. Off, they zoom toward the middle of the map.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ZoomMapInHotkey = config.Bind(
                GeneralTitle,
                "Zoom in",
                new KeyboardShortcut(KeyCode.Equals),
                new ConfigDescription(
                    "Zoom closer. Scrolling up does the same on the map screen.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ZoomMapOutHotkey = config.Bind(
                GeneralTitle,
                "Zoom out",
                new KeyboardShortcut(KeyCode.Minus),
                new ConfigDescription(
                    "Zoom further out. Scrolling down does the same on the map screen.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ZoomMapHotkeySpeed = config.Bind(
                GeneralTitle,
                "Zoom key speed",
                2.5f,
                new ConfigDescription(
                    "How fast the zoom-in and zoom-out keys change the zoom. 1 is gentle, 10 jumps.",
                    new AcceptableValueRange<float>(1f, 10f),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(DumpInfoHotkey = config.Bind(
                GeneralTitle,
                "Export marker positions",
                new KeyboardShortcut(KeyCode.D, KeyCode.LeftShift, KeyCode.LeftAlt),
                new ConfigDescription(
                    "While the map is open, write extract, loot, and switch positions as JSON into the Maps Expanded plugin folder. Meant for people making map packs.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true })));

            #endregion

            #region Markers

            var addonsPath = Path.Combine(Plugin.Path, "Addons");
            var markerPacks = new List<string> { "Default" };
            if (Directory.Exists(addonsPath))
            {
                markerPacks.AddRange(Directory.EnumerateDirectories(addonsPath)
                    .Where(folder => Directory.Exists(Path.Combine(folder, "Markers")))
                    .Select(folder => Path.GetFileName(folder))
                    .Where(name => !name.Equals("Default", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            }

            ConfigEntries.Add(MarkerPack = config.Bind(
                DynamicMarkerTitle,
                "Icon set",
                "Default",
                new ConfigDescription(
                    "Which folder of marker icons to use. An icon missing from that set falls back to Default. Restart the game after adding a new addon icon pack.",
                    new AcceptableValueList<string>(markerPacks.ToArray()),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowPlayerMarker = config.Bind(
                DynamicMarkerTitle,
                "Show you",
                true,
                new ConfigDescription(
                    "Draw your own arrow during a raid.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowFriendlyPlayerMarkersInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show teammates",
                true,
                new ConfigDescription(
                    "Draw players who are grouped with you.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowEnemyPlayerMarkersInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show enemy PMCs",
                false,
                new ConfigDescription(
                    "Draw hostile PMCs. This reveals positions you would not normally know.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowScavMarkersInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show scavs",
                false,
                new ConfigDescription(
                    "Draw hostile scavs. This reveals positions you would not normally know.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowBossMarkersInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show living bosses",
                false,
                new ConfigDescription(
                    "Draw bosses that are still alive.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowNonVanillaBotMarkersInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show modded bots",
                true,
                new ConfigDescription(
                    "Draw bots added by other mods, including custom factions.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(NonVanillaBotUseStar = config.Bind(
                DynamicMarkerTitle,
                "Star icon for modded bots",
                true,
                new ConfigDescription(
                    "Modded bots use a star. Turn this off and they use the same arrow as everyone else.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowLockedDoorStatus = config.Bind(
                DynamicMarkerTitle,
                "Colour locks by key",
                true,
                new ConfigDescription(
                    "Recolour locked doors, keycards, and safes based on whether the key is on you, in your stash, or missing.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowQuestsInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show quest objectives",
                true,
                new ConfigDescription(
                    "Mark unfinished objectives in raid and in the hideout. A hideout map only gets those pins after you have loaded into that location once, so the zone positions can be saved.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowExtractsInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show extracts",
                true,
                new ConfigDescription(
                    "Mark extraction points during a raid.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowExtractStatusInRaid = config.Bind(
                DynamicMarkerTitle,
                "Colour extracts by status",
                true,
                new ConfigDescription(
                    "Open, closed, and requirement extracts use the colours from the Colours section. Hover one that is not open yet to see what it still needs.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowTransitPointsInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show transits",
                true,
                new ConfigDescription(
                    "Mark transit locations during a raid.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowSecretPointsInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show secret extracts",
                true,
                new ConfigDescription(
                    "Mark secret and code-word extracts during a raid.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowDroppedBackpackInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show bags you dropped",
                true,
                new ConfigDescription(
                    "Mark backpacks you threw on the ground. Other people's bags stay hidden.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowBTRInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show the BTR",
                true,
                new ConfigDescription(
                    "Mark the BTR while it is driving the route.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowAirdropsInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show airdrops",
                true,
                new ConfigDescription(
                    "Mark an airdrop after the crate has landed.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowHiddenStashesInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show hidden stashes",
                true,
                new ConfigDescription(
                    "Mark buried barrel caches.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowFriendlyCorpsesInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show teammate bodies",
                true,
                new ConfigDescription(
                    "Mark the bodies of players who were grouped with you.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowKilledCorpsesInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show bodies you killed",
                true,
                new ConfigDescription(
                    "Mark people you killed. Bosses you killed use the boss-body colour.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowFriendlyKilledCorpsesInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show bodies your team killed",
                true,
                new ConfigDescription(
                    "Mark people a teammate killed. Bosses they killed use the boss-body colour.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowBossCorpsesInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show boss bodies",
                false,
                new ConfigDescription(
                    "Mark dead bosses that you and your team did not kill.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ShowOtherCorpsesInRaid = config.Bind(
                DynamicMarkerTitle,
                "Show other bodies",
                false,
                new ConfigDescription(
                    "Mark dead players and scavs that are not teammates and were not killed by you or your team.",
                    null,
                    new ConfigurationManagerAttributes())));

            #endregion

            #region In Raid

            ConfigEntries.Add(AutoSelectLevel = config.Bind(
                InRaidTitle,
                "Follow your current floor",
                true,
                new ConfigDescription(
                    "Switch the displayed floor to the height you are standing at.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(AutoCenterOnPlayerMarker = config.Bind(
                InRaidTitle,
                "Snap back to you",
                true,
                new ConfigDescription(
                    "Each time you open the map in a raid, center it on you. Turn this off to keep the view where you left it.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ResetZoomOnCenter = config.Bind(
                InRaidTitle,
                "Reset zoom when centering",
                false,
                new ConfigDescription(
                    "When the map recenters on you, also jump to the zoom set below. Turning this on enables centering and turns off keeping the last view.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(RetainMapPosition = config.Bind(
                InRaidTitle,
                "Keep the last view",
                false,
                new ConfigDescription(
                    "Reopen the map where you left it, for this raid only. Turning this on disables centering and zoom reset.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(CenteringZoomResetPoint = config.Bind(
                InRaidTitle,
                "Zoom after a reset",
                0.15f,
                new ConfigDescription(
                    "Zoom used when reset-on-center is on. 0 is the whole map, 1 is as close as the map goes.",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ZoomMainMap = config.Bind(
                InRaidTitle,
                "Starting zoom in raid",
                0f,
                new ConfigDescription(
                    "Zoom used the first time you open the raid map or a peek, unless reset-on-center is on. Hideout maps always open fitted to the screen. 0 is the whole map, 1 is as close as the map goes.",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(PeekShortcut = config.Bind(
                InRaidTitle,
                "Peek key",
                new KeyboardShortcut(KeyCode.M),
                new ConfigDescription(
                    "Bring the map up over the raid without opening your inventory.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(HoldForPeek = config.Bind(
                InRaidTitle,
                "Hold for checking map",
                true,
                new ConfigDescription(
                    "Hold the check map key to keep the map up. Turn this off and the key toggles the peek open and closed.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MapTransitionEnabled = config.Bind(
                InRaidTitle,
                "Animate view changes",
                true,
                new ConfigDescription(
                    "Glide between the full map, the peek, and the corner map. Off, those views jump into place.",
                    null,
                    new ConfigurationManagerAttributes())));

            #endregion

            AutoCenterOnPlayerMarker.SettingChanged += OnAutoCenterOnPlayerEnable;
            ResetZoomOnCenter.SettingChanged += OnResetZoomEnable;
            RetainMapPosition.SettingChanged += OnPositionRetainEnable;
            ZoomMainMap.SettingChanged += ZoomMainMapChanged;
            
            #region Mini Map

            ConfigEntries.Add(MiniMapEnabled = config.Bind(
                MiniMapTitle,
                "Show the corner map",
                true,
                new ConfigDescription(
                    "Keep a small map on screen during a raid.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MiniMapPosition = config.Bind(
                MiniMapTitle,
                "Corner",
                EMiniMapPosition.TopRight,
                new ConfigDescription(
                    "Which corner of the screen the small map sits in.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MiniMapSizeX = config.Bind(
                MiniMapTitle,
                "Width",
                275.0f,
                new ConfigDescription(
                    "Width of the corner map, in pixels.",
                    new AcceptableValueRange<float>(0f, 850f),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MiniMapSizeY = config.Bind(
                MiniMapTitle,
                "Height",
                275.0f,
                new ConfigDescription(
                    "Height of the corner map, in pixels.",
                    new AcceptableValueRange<float>(0f, 850f),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MiniMapScreenOffsetX = config.Bind(
                MiniMapTitle,
                "Nudge sideways",
                0.0f,
                new ConfigDescription(
                    "Extra pixels in from the side of the screen. If you change resolution, set this again and restart the game.",
                    new AcceptableValueRange<float>(-Screen.width / 4f, Screen.width),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MiniMapScreenOffsetY = config.Bind(
                MiniMapTitle,
                "Nudge up or down",
                0.0f,
                new ConfigDescription(
                    "Extra pixels in from the top or bottom of the screen. If you change resolution, set this again and restart the game.",
                    new AcceptableValueRange<float>(-Screen.height / 4f, Screen.height),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(MiniMapShowOrHide = config.Bind(
                MiniMapTitle,
                "Show or hide key",
                new KeyboardShortcut(KeyCode.End),
                new ConfigDescription(
                    "Toggle the corner map without opening the full map.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ZoomMiniMap = config.Bind(
                MiniMapTitle,
                "Corner map zoom",
                0.33f,
                new ConfigDescription(
                    "How close the corner map is. 0 shows the whole map, 1 is as close as it goes.",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ZoomInMiniMapHotkey = config.Bind(
                MiniMapTitle,
                "Zoom the corner map in",
                new KeyboardShortcut(KeyCode.Keypad8),
                new ConfigDescription(
                    "Move the corner map closer.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ZoomOutMiniMapHotkey = config.Bind(
                MiniMapTitle,
                "Zoom the corner map out",
                new KeyboardShortcut(KeyCode.Keypad5),
                new ConfigDescription(
                    "Pull the corner map back.",
                    null,
                    new ConfigurationManagerAttributes())));
            
            ZoomMiniMap.SettingChanged += ZoomMiniMapChanged;
            
            #endregion

            #region MarkerColors

            ConfigEntries.Add(PlayerColor = config.Bind(
                MarkerColors,
                "Your arrow",
                new Color(0, 1, 0),
                new ConfigDescription(
                    "Colour of your own marker.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(FriendlyPlayerColor = config.Bind(
                MarkerColors,
                "Teammate arrows",
                new Color(0.5f, 0.5f, 1f),
                new ConfigDescription(
                    "Colour of players grouped with you.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(PmcBearColor = config.Bind(
                MarkerColors,
                "Bear arrows",
                new Color(1, 0, 0),
                new ConfigDescription(
                    "Colour of Bear PMCs.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(PmcUsecColor = config.Bind(
                MarkerColors,
                "USEC arrows",
                new Color(1, 1, 0),
                new ConfigDescription(
                    "Colour of USEC PMCs.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ScavColor = config.Bind(
                MarkerColors,
                "Scav arrows",
                new Color(1, 0.45f, 0.007f),
                new ConfigDescription(
                    "Colour of scavs.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(BossColor = config.Bind(
                MarkerColors,
                "Boss arrows",
                new Color(1f, 0.45f, 0.007f),
                new ConfigDescription(
                    "Colour of living bosses.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(NonVanillaBotColor = config.Bind(
                MarkerColors,
                "Modded bot arrows",
                new Color(0.33f, 0.15f, 0.15f),
                new ConfigDescription(
                    "Colour of bots added by other mods, when a faction tint is not used.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(AirdropColor = config.Bind(
                MarkerColors,
                "Airdrop crates",
                new Color(1f, 0.30f, 0.007f),
                new ConfigDescription(
                    "Colour of landed airdrop crates.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(BackpackColor = config.Bind(
                MarkerColors,
                "Bags you dropped",
                new Color(0f, 1f, 0.0f),
                new ConfigDescription(
                    "Colour of backpacks you threw on the ground.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(KilledCorpseColor = config.Bind(
                MarkerColors,
                "Bodies you killed",
                new Color(1f, 0.0f, 0.0f),
                new ConfigDescription(
                    "Colour of people you killed, other than bosses.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(KilledBossColor = config.Bind(
                MarkerColors,
                "Boss bodies",
                new Color(1f, 0.0f, 1.0f),
                new ConfigDescription(
                    "Colour of dead bosses, including ones you or a teammate killed.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(KilledOtherColor = config.Bind(
                MarkerColors,
                "Other bodies",
                new Color(1f, 1f, 1f),
                new ConfigDescription(
                    "Colour of bodies that are not teammates and were not killed by your group.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(FriendlyCorpseColor = config.Bind(
                MarkerColors,
                "Teammate bodies",
                new Color(0.5f, 0.5f, 1f),
                new ConfigDescription(
                    "Colour of dead players who were grouped with you.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(FriendlyKilledCorpseColor = config.Bind(
                MarkerColors,
                "Bodies your team killed",
                new Color(0.75f, 0.25f, 0.5f),
                new ConfigDescription(
                    "Colour of people a teammate killed, other than bosses.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(BtrColor = config.Bind(
                MarkerColors,
                "The BTR",
                new Color(0.21f, 0.39f, 0.16f),
                new ConfigDescription(
                    "Colour of the BTR.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ExtractDefaultColor = config.Bind(
                MarkerColors,
                "Extract, status off",
                new Color(1f, 0.92f, 0.01f),
                new ConfigDescription(
                    "Colour of extracts when status colours are turned off.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ExtractOpenColor = config.Bind(
                MarkerColors,
                "Extract you can use",
                new Color(0f, 1f, 0f),
                new ConfigDescription(
                    "Colour of an extract that is open.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ExtractClosedColor = config.Bind(
                MarkerColors,
                "Extract that is shut",
                new Color(1f, 0f, 0f),
                new ConfigDescription(
                    "Colour of an extract that is closed.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(ExtractHasRequirementsColor = config.Bind(
                MarkerColors,
                "Extract that still needs something",
                new Color(1f, 0.92f, 0.01f),
                new ConfigDescription(
                    "Colour of an extract that is waiting on a switch, payment, or note.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(TransPointColor = config.Bind(
                MarkerColors,
                "Transits",
                new Color(1f, 0.62f, 0.20f),
                new ConfigDescription(
                    "Colour of transit points.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(SecretPointColor = config.Bind(
                MarkerColors,
                "Secret extracts",
                new Color(0.1f, 0.6f, 0.6f),
                new ConfigDescription(
                    "Colour of secret and code-word extracts.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(HiddenStashColor = config.Bind(
                MarkerColors,
                "Hidden stashes",
                new Color(1f, 0.92f, 0.01f),
                new ConfigDescription(
                    "Colour of buried barrel caches.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(QuestColor = config.Bind(
                MarkerColors,
                "Quest objectives",
                Color.green,
                new ConfigDescription(
                    "Colour of unfinished quest pins.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(LockedDoorHasKeyColor = config.Bind(
                MarkerColors,
                "Lock, key is on you",
                Color.green,
                new ConfigDescription(
                    "Colour of a locked door, keycard door, or safe when you are carrying the key.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(LockedDoorInStashColor = config.Bind(
                MarkerColors,
                "Lock, key is in your stash",
                Color.yellow,
                new ConfigDescription(
                    "Colour of a lock when the key is in your stash and not on you.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(LockedDoorMissingKeyColor = config.Bind(
                MarkerColors,
                "Lock, key missing",
                Color.red,
                new ConfigDescription(
                    "Colour of a lock when you do not have the key.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(LockedDoorDefaultColor = config.Bind(
                MarkerColors,
                "Lock, key colours off",
                Color.yellow,
                new ConfigDescription(
                    "Colour of every lock when key colouring is turned off.",
                    null,
                    new ConfigurationManagerAttributes())));

            ConfigEntries.Add(HeliCrashColor = config.Bind(
                MarkerColors,
                "Crashed helicopter",
                Color.Lerp(Color.red, Color.white, 0.333f),
                new ConfigDescription(
                    "Colour of a SamSWAT HeliCrash site. Shown only when that mod is installed.",
                    null,
                    new ConfigurationManagerAttributes
                    {
                        Browsable = ModDetection.HeliCrashLoaded
                    })));
            
            #endregion
            
            #region ExternalModSupport

            ConfigEntries.Add(ShowHeliCrashMarker = config.Bind(
                ExternModSupport,
                "Mark helicopter crashes",
                true,
                new ConfigDescription(
                    "Draw the SamSWAT HeliCrash site during a raid. This entry only appears when that mod is installed.",
                    null,
                    new ConfigurationManagerAttributes {
                        Browsable = ModDetection.HeliCrashLoaded
                    })));
            
            #endregion
            
            RecalcOrder();
        }
        
        public static void RegisterMapPreference(string location, IEnumerable<string> maps)
        {
            var choices = new[] { "Automatic" }.Concat(maps).ToArray();
            var selected = "Automatic";
            if (MapPreferences.TryGetValue(location, out var existing))
            {
                var acceptable = (AcceptableValueList<string>)existing.Description.AcceptableValues;
                if (acceptable.AcceptableValues.SequenceEqual(choices))
                {
                    return;
                }
                
                selected = existing.Value;
                _config.Remove(existing.Definition);
                ConfigEntries.Remove(existing);
            }

            var preference = _config.Bind(MapSelectionTitle, Uri.EscapeDataString(location),
                "Automatic", new ConfigDescription(
                    "Art used for this location. Automatic picks a bundled map first, then a matching addon. The new art shows the next time that map opens.",
                    new AcceptableValueList<string>(choices),
                    new ConfigurationManagerAttributes()));
            
            if (choices.Contains(selected) && selected != "Automatic")
            {
                preference.Value = selected;
            }
            
            MapPreferences[location] = preference;
            ConfigEntries.Add(preference);
            RecalcOrder();
        }

        public static string GetPreferredMap(string location)
        {
            return MapPreferences.TryGetValue(location, out var preference) ? preference.Value : "Automatic";
        }

        private static void RecalcOrder()
        {
            // Set the Order field for all settings, to avoid unnecessary changes when adding new settings
            var settingOrder = ConfigEntries.Count;
            foreach (var entry in ConfigEntries)
            {
                var attributes = entry.Description.Tags[0] as ConfigurationManagerAttributes;
                if (attributes != null)
                {
                    attributes.Order = settingOrder;
                }

                settingOrder--;
            }
        }
        
        private static void OnAutoCenterOnPlayerEnable(object sender, EventArgs e)
        {
            if (AutoCenterOnPlayerMarker.Value)
            {
                RetainMapPosition.Value = false;
            }
            else
            {
                ResetZoomOnCenter.Value = false;
            }
        }

        private static void OnPositionRetainEnable(object sender, EventArgs e)
        {
            if (RetainMapPosition.Value)
            {
                AutoCenterOnPlayerMarker.Value = false;
                ResetZoomOnCenter.Value = false;
            }
        }

        private static void OnResetZoomEnable(object sender, EventArgs e)
        {
            if (ResetZoomOnCenter.Value)
            {
                AutoCenterOnPlayerMarker.Value = true;
                RetainMapPosition.Value = false;
            }
        }

        public static event Action<float> OnZoomMainMapChanged;
        private static void ZoomMainMapChanged(object sender, EventArgs e)
        {
            if (ZoomMainMap == null) 
                return;
            OnZoomMainMapChanged?.Invoke(ZoomMainMap.Value);
        }

        public static event Action<float> OnZoomMiniMapChanged;
        private static void ZoomMiniMapChanged(object sender, EventArgs e)
        {
            if (ZoomMiniMap == null) 
                return;
            OnZoomMiniMapChanged?.Invoke(ZoomMiniMap.Value);
        }
    }
}
