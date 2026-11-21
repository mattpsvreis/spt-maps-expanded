using System;
using System.Collections.Generic;
using System.IO;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using MapsExtended.Integration;
using Newtonsoft.Json;
using UnityEngine;

namespace MapsExtended.Core
{
    public static class DumpUtils
    {
        private const string ExtractCategory = "Extract";
        private const string ExtractImagePath = "Markers/exit.png";

        private const string SecretCategory = "Secret";
        private const string SecretImagePath = "Markers/exit.png";

        private const string TransitCategory = "Transit";
        private const string TransitImagePath = "Makers/transit.png";

        private static readonly Color ExtractScavColor = Color.Lerp(Color.yellow, Color.red, 0.5f);
        private static readonly Color TransitColor = Color.Lerp(Color.yellow, Color.red, 0.6f);
        private static readonly Color SecretColor = new Color(0.1f, 0.6f, 0.6f);
        private static readonly Color ExtractPmcColor = Color.green;

        private const string SwitchCategory = "Switch";
        private const string SwitchImagePath = "Markers/lever.png";

        private const string LockedDoorCategory = "Locked Door";
        private const string LockedDoorImagePath = "Markers/door_with_lock.png";
        private const string LockedSafeImagePath = "Markers/safe_with_lock.png";
        private const string LockedKeycardImagePath = "Markers/door_keycard_with_lock.png";
        private static readonly Color LockedDoorColor = Color.yellow;

        public static void DumpExtracts()
        {
            var world = Singleton<GameWorld>.Instance;
            var controller = world.ExfiltrationController;
            var markers = new List<MapMarkerDef>();

            AppendExfils(markers, controller.ScavExfiltrationPoints, ExtractScavColor, ExtractCategory, ExtractImagePath);
            AppendExfils(markers, controller.ExfiltrationPoints, ExtractPmcColor, ExtractCategory, ExtractImagePath);
            AppendTransits(markers);
            AppendExfils(markers, controller.SecretExfiltrationPoints, SecretColor, SecretCategory, SecretImagePath);

            WriteDump(markers, "extracts");
            Plugin.Log.LogInfo("Dumped extracts");
        }

        public static void DumpSwitches()
        {
            var markers = new List<MapMarkerDef>();
            foreach (var lever in GameObject.FindObjectsOfType<Switch>())
            {
                if (!lever.Operatable || !lever.HasAuthority)
                {
                    continue;
                }

                markers.Add(Make(SwitchCategory, SwitchImagePath, lever.name, lever.transform));
            }

            WriteDump(markers, "switches");
            Plugin.Log.LogInfo("Dumped switches");
        }

        public static void DumpLocks()
        {
            var markers = new List<MapMarkerDef>();
            var doorNumber = 1;
            foreach (var interactive in GameObject.FindObjectsOfType<WorldInteractiveObject>())
            {
                if (string.IsNullOrEmpty(interactive.KeyId) || !interactive.Operatable)
                {
                    continue;
                }

                var safe = interactive is LootableContainer && LooksLikeSafe(interactive.name);
                var keycard = !safe && KeyNameMentionsCard(interactive.KeyId);
                var image = LockedDoorImagePath;
                if (safe)
                {
                    image = LockedSafeImagePath;
                }
                else if (keycard)
                {
                    image = LockedKeycardImagePath;
                }

                var label = interactive.name;
                if (!safe)
                {
                    label = "door " + doorNumber;
                    doorNumber++;
                }

                var marker = Make(LockedDoorCategory, image, label, interactive.transform);
                marker.AssociatedItemId = interactive.KeyId;
                marker.Color = LockedDoorColor;
                markers.Add(marker);
            }

            WriteDump(markers, "locked");
            Plugin.Log.LogInfo("Dumped locks");
        }

        private static void AppendExfils(List<MapMarkerDef> markers, IEnumerable<ExfiltrationPoint> points, Color color, string category, string image)
        {
            if (points == null)
            {
                return;
            }

            foreach (var point in points)
            {
                var marker = Make(category, image, point.Settings.Name.BsgLocalized(), point.transform);
                marker.ShowInRaid = false;
                marker.Color = color;
                markers.Add(marker);
            }
        }

        private static void AppendTransits(List<MapMarkerDef> markers)
        {
            foreach (var transit in LocationScene.GetAllObjects<TransitPoint>(true))
            {
                var marker = Make(TransitCategory, TransitImagePath, transit.parameters.description.BsgLocalized(), transit.transform);
                marker.ShowInRaid = false;
                marker.Color = TransitColor;
                markers.Add(marker);
            }
        }

        private static MapMarkerDef Make(string category, string image, string text, Transform transform)
        {
            return new MapMarkerDef
            {
                Category = category,
                ImagePath = image,
                Text = text,
                Position = MathUtils.ConvertToMapPosition(transform)
            };
        }

        private static void WriteDump(List<MapMarkerDef> markers, string suffix)
        {
            var mapName = GameUtils.GetCurrentMapInternalName();
            var json = JsonConvert.SerializeObject(markers, Formatting.Indented);
            var file = Path.Combine(Plugin.Path, mapName + "-" + suffix + ".json");
            File.WriteAllText(file, json);
        }

        private static bool LooksLikeSafe(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            var hasSafe = name.IndexOf("safe", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasCash = name.IndexOf("cash", StringComparison.OrdinalIgnoreCase) >= 0;
            return hasSafe && !hasCash;
        }

        private static bool KeyNameMentionsCard(string keyId)
        {
            var localized = (keyId + " Name").BsgLocalized();
            return localized.IndexOf("keycard", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
