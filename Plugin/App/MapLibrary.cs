using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapsExtended.Core;

namespace MapsExtended.App
{
    internal static class MapLibrary
    {
        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".json", ".jsonc" };
        private static readonly Dictionary<string, DateTime> Writes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, MapDef> Maps = new(StringComparer.OrdinalIgnoreCase);

        internal static IReadOnlyDictionary<string, MapDef> Current => Maps;

        internal static void Refresh(string pluginRoot)
        {
            var found = Find(pluginRoot);
            foreach (var gone in Maps.Keys.Where(path => !found.ContainsKey(path)).ToList())
            {
                Maps.Remove(gone);
                Writes.Remove(gone);
            }

            foreach (var pair in found.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                var written = File.GetLastWriteTimeUtc(pair.Key);
                if (Writes.TryGetValue(pair.Key, out var previous) && previous == written && Maps.ContainsKey(pair.Key))
                {
                    continue;
                }

                var map = MapDef.LoadFromPath(pair.Key, pair.Value);
                if (map == null)
                {
                    Maps.Remove(pair.Key);
                    Writes.Remove(pair.Key);
                    continue;
                }

                Writes[pair.Key] = written;
                Maps[pair.Key] = map;
            }
        }

        private static Dictionary<string, string> Find(string pluginRoot)
        {
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var bundled = Path.Combine(pluginRoot, "Maps");
            if (Directory.Exists(bundled))
            {
                foreach (var file in Directory.EnumerateFiles(bundled, "*.*", SearchOption.AllDirectories))
                {
                    if (Extensions.Contains(Path.GetExtension(file)))
                    {
                        found[file] = null;
                    }
                }
            }

            var addons = Path.Combine(pluginRoot, "Addons");
            if (!Directory.Exists(addons))
            {
                return found;
            }

            foreach (var pack in Directory.EnumerateDirectories(addons))
            {
                foreach (var mapDir in Directory.EnumerateDirectories(pack))
                {
                    foreach (var file in Directory.EnumerateFiles(mapDir))
                    {
                        if (Extensions.Contains(Path.GetExtension(file)))
                        {
                            found[file] = Path.GetFileName(pack);
                        }
                    }
                }
            }

            return found;
        }
    }
}
