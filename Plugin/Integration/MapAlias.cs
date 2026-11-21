using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapsExtended.Integration
{
    /// <summary>
    /// Picks the Extended addon pack when Map Variants or Manimal/Classic ports are active.
    /// Live location ids used by markers stay unchanged.
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
        private static readonly Dictionary<string, bool> PackCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> MissingWarned = new(StringComparer.OrdinalIgnoreCase);

        internal static string Resolve(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId) || AlreadyAlias(locationId))
            {
                return locationId;
            }

            var requested = locationId;
            if (FactoryClassicBridge.IsFactory(locationId))
            {
                return ResolveFactory(requested);
            }

            return ResolvePort(requested);
        }

        private static string ResolveFactory(string requested)
        {
            var classic = FactoryClassicBridge.IsClassicChosen || FactoryClassicBridge.ClassicSceneLoaded;
            var ready = PackReady("Factory_Classic");
            var result = requested;
            if (classic && ready)
            {
                result = FactoryClassicAlias;
            }
            else if (classic && !ready)
            {
                WarnOnce("Factory_Classic", "Classic Maps ports by LennoxP90 is active, but Addons/Extended/Factory_Classic is missing.");
            }

            LogOnce($"factory|{requested}|{classic}|{ready}|{result}",
                $"Alias: factory requested={requested}, classic={classic}, ready={ready}, result={result}");
            return result;
        }

        private static string ResolvePort(string requested)
        {
            var normalized = requested.Trim().ToLowerInvariant();
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
                    return requested;
            }

            var variants = MapVariantsBridge.Resolve(requested);
            var packReady = PackReady(packFolder);
            var backportReady = PluginDetection.Loaded(pluginGuid) && packReady;
            var result = requested;

            if (variants.ApiAvailable && variants.Managed)
            {
                if (variants.Decided && variants.IsBackport && backportReady)
                {
                    result = alias;
                }
                else if (variants.Decided && variants.IsBackport && !backportReady)
                {
                    WarnOnce(packFolder,
                        $"MapVariants selected backport for {requested}, but Extended pack / plugin is unavailable ({pluginGuid}).");
                }

                LogOnce($"managed|{requested}|{variants}|{backportReady}|{result}",
                    $"Alias: requested={requested}, MapVariants({variants}), ready={backportReady}, result={result}");
                return result;
            }

            if (normalized == "laboratory" && backportReady)
            {
                result = alias;
            }

            LogOnce($"fallback|{requested}|{variants}|{backportReady}|{result}",
                $"Alias: requested={requested}, MapVariants({variants}), ready={backportReady}, result={result}");
            return result;
        }

        private static bool AlreadyAlias(string locationId)
        {
            return locationId.Equals(InterchangeAlias, StringComparison.OrdinalIgnoreCase)
                   || locationId.Equals(LabsAlias, StringComparison.OrdinalIgnoreCase)
                   || locationId.Equals(FactoryClassicAlias, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PackReady(string folderName)
        {
            if (PackCache.TryGetValue(folderName, out var cached))
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
                    ready = hasJson && Directory.Exists(layers) && Directory.EnumerateFiles(layers).Any();
                }
            }
            catch
            {
                ready = false;
            }

            PackCache[folderName] = ready;
            return ready;
        }

        private static void WarnOnce(string key, string message)
        {
            if (MissingWarned.Add(key))
            {
                Plugin.Log?.LogWarning(message);
            }
        }

        private static void LogOnce(string key, string message)
        {
            if (Plugin.DebugEnabled && Logged.Add(key))
            {
                Plugin.Log?.LogInfo("[MapsExpanded:DBG] " + message);
            }
        }
    }
}
