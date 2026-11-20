using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapsExtended.Integration
{
    /// <summary>
    /// Redirects dropdown filter / preferred-map lookup onto Extended addon packs
    /// when Map Variants / Classic Maps ports by LennoxP90 / Manimal backports are active.
    /// Live location IDs used by markers stay untouched.
    /// </summary>
    internal static class MapAlias
    {
        internal const string InterchangeAlias = "mapsext_interchange_backport";
        internal const string LabsAlias = "mapsext_laboratory_backport";
        internal const string FactoryClassicAlias = "mapsext_factory_classic";

        internal const string MapVariantsGuid = "com.lennoxp90.mapvariants";
        internal const string FactoryClassicGuid = "com.lennoxp90.factoryclassic";
        internal const string ManimalInterchangeGuid = "com.manimal.interchange";
        internal const string ManimalLabsGuid = "com.manimal.labsboiler";

        private static readonly HashSet<string> Logged = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool> PackReadyCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> MissingPackWarned = new(StringComparer.OrdinalIgnoreCase);

        internal static string Resolve(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return locationId;
            }

            // Already an Extended alias — leave alone (idempotent for filter + preferred).
            if (IsExtendedAlias(locationId))
            {
                return locationId;
            }

            var requested = locationId;

            if (FactoryClassicBridge.IsFactory(locationId))
            {
                var classic = FactoryClassicBridge.IsClassicChosen || FactoryClassicBridge.ClassicSceneLoaded;
                var ready = PackReady("Factory_Classic");
                if (classic && ready)
                {
                    locationId = FactoryClassicAlias;
                }
                else if (classic && !ready)
                {
                    WarnMissingPackOnce("Factory_Classic", "Classic Maps ports by LennoxP90 is active, but Addons/Extended/Factory_Classic is missing.");
                }

                LogOnce($"factory|{requested}|{classic}|{ready}|{locationId}",
                    $"Alias: factory requested={requested}, classic={classic}, ready={ready}, result={locationId}");
                return locationId;
            }

            var normalized = locationId.Trim().ToLowerInvariant();
            string alias;
            string pluginGuid;
            string packFolder;

            switch (normalized)
            {
                case "interchange":
                    alias = InterchangeAlias;
                    pluginGuid = ManimalInterchangeGuid;
                    packFolder = "Interchange_Manimal";
                    break;
                case "laboratory":
                    alias = LabsAlias;
                    pluginGuid = ManimalLabsGuid;
                    packFolder = "Labs_Satellite";
                    break;
                default:
                    return locationId;
            }

            var mv = MapVariantsBridge.Resolve(locationId);
            var packReady = PackReady(packFolder);
            var backportReady = PluginDetection.Loaded(pluginGuid) && packReady;

            if (mv.ApiAvailable && mv.Managed)
            {
                if (mv.Decided && mv.IsBackport && backportReady)
                {
                    locationId = alias;
                }
                else if (mv.Decided && mv.IsBackport && !backportReady)
                {
                    WarnMissingPackOnce(packFolder,
                        $"MapVariants selected backport for {requested}, but Extended pack / plugin is unavailable ({pluginGuid}).");
                }

                LogOnce($"managed|{requested}|{mv}|{backportReady}|{locationId}",
                    $"Alias: requested={requested}, MapVariants({mv}), ready={backportReady}, result={locationId}");
                return locationId;
            }

            // Labs Boiler often forces the boiler layout even without MapVariants owning the location.
            if (normalized == "laboratory" && backportReady)
            {
                locationId = alias;
            }

            LogOnce($"fallback|{requested}|{mv}|{backportReady}|{locationId}",
                $"Alias: requested={requested}, MapVariants({mv}), ready={backportReady}, result={locationId}");
            return locationId;
        }

        private static bool IsExtendedAlias(string locationId)
            => locationId.Equals(InterchangeAlias, StringComparison.OrdinalIgnoreCase)
               || locationId.Equals(LabsAlias, StringComparison.OrdinalIgnoreCase)
               || locationId.Equals(FactoryClassicAlias, StringComparison.OrdinalIgnoreCase);

        private static bool PackReady(string folderName)
        {
            if (PackReadyCache.TryGetValue(folderName, out var cached))
            {
                return cached;
            }

            var ready = false;
            try
            {
                var root = Path.Combine(Plugin.Path, "Addons", "Extended", folderName);
                if (Directory.Exists(root))
                {
                    var hasJson = Directory.EnumerateFiles(root, "*.jsonc").Any()
                                  || Directory.EnumerateFiles(root, "*.json").Any();
                    var layers = Path.Combine(root, "Layers");
                    var hasLayers = Directory.Exists(layers) && Directory.EnumerateFiles(layers).Any();
                    ready = hasJson && hasLayers;
                }
            }
            catch
            {
                ready = false;
            }

            PackReadyCache[folderName] = ready;
            return ready;
        }

        private static void WarnMissingPackOnce(string key, string message)
        {
            if (MissingPackWarned.Add(key))
            {
                Plugin.Log?.LogWarning(message);
            }
        }

        private static void LogOnce(string key, string message)
        {
            if (!Plugin.DebugEnabled || !Logged.Add(key))
            {
                return;
            }

            Plugin.Log?.LogInfo("[MapsExtended:DBG] " + message);
        }
    }
}
