using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MapsExtended.Core
{
    public class BoundingRectangle
    {
        public Vector2 Min { get; set; }
        public Vector2 Max { get; set; }
    }

    public class BoundingRectangularSolid
    {
        public Vector3 Min { get; set; }
        public Vector3 Max { get; set; }
    }

    public class MapLayerDef
    {
        [JsonRequired]
        public int Level { get; set; }

        [JsonRequired]
        public string ImagePath { get; set; }

        public int TesselationIndex { get; set; }

        [JsonRequired]
        public BoundingRectangle ImageBounds { get; set; }

        [JsonRequired]
        public List<BoundingRectangularSolid> GameBounds { get; set; }
    }

    public class MapMarkerDef
    {
        public string Text { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [JsonRequired]
        public string ImagePath { get; set; }

        [JsonRequired]
        public Vector3 Position { get; set; }

        public Sprite Sprite { get; set; }
        public bool ShowInRaid { get; set; } = true;
        public string Category { get; set; } = "None";
        public Color Color { get; set; } = Color.white;
        public Vector2 Pivot { get; set; } = new Vector2(0.5f, 0.5f);
        public string AssociatedItemId { get; set; } = string.Empty;
    }

    public class MapLabelDef
    {
        [JsonRequired]
        public string Text { get; set; }

        [JsonRequired]
        public Vector3 Position { get; set; }

        public float FontSize { get; set; } = 14f;
        public float DegreesRotation { get; set; }
        public Color Color { get; set; } = Color.white;
        public string Category { get; set; } = "None";
    }

    public class MapDef
    {
        [JsonRequired]
        public string DisplayName { get; set; }

        [JsonRequired]
        public BoundingRectangle Bounds { get; set; }

        [JsonRequired]
        public Dictionary<string, MapLayerDef> Layers { get; set; } = new Dictionary<string, MapLayerDef>();

        public List<string> MapInternalNames { get; set; } = new List<string>();
        public float CoordinateRotation { get; set; }
        public List<MapLabelDef> Labels { get; set; } = new List<MapLabelDef>();
        public List<MapMarkerDef> StaticMarkers { get; set; } = new List<MapMarkerDef>();
        public int DefaultLevel { get; set; }

        [JsonIgnore]
        public string SelectionId { get; set; }

        [JsonIgnore]
        public bool IsAddon { get; set; }

        public string Author { get; set; }
        public string AuthorLink { get; set; }

        public static MapDef LoadFromPath(string absolutePath, string addonName = null)
        {
            try
            {
                var text = File.ReadAllText(absolutePath);
                var loaded = JsonConvert.DeserializeObject<MapDef>(text);
                EnsurePackIsUsable(loaded);
                StampIdentity(loaded, absolutePath, addonName);
                ResolveLayerFiles(loaded, absolutePath);
                return loaded;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Loading MapMappingDef failed from json at path: {absolutePath}");
                Plugin.Log.LogError($"Exception given was: {ex.Message}");
                Plugin.Log.LogError($"{ex.StackTrace}");
                return null;
            }
        }

        private static void EnsurePackIsUsable(MapDef map)
        {
            if (map == null
                || map.Bounds == null
                || map.Layers == null
                || map.Layers.Count == 0
                || map.MapInternalNames == null
                || string.IsNullOrWhiteSpace(map.DisplayName))
            {
                throw new InvalidDataException("Incomplete map definition");
            }
        }

        private static void StampIdentity(MapDef map, string absolutePath, string addonName)
        {
            var relative = Path.GetRelativePath(Plugin.Path, absolutePath);
            map.SelectionId = relative.Replace('\\', '/');
            map.IsAddon = addonName != null;
            if (!map.IsAddon)
            {
                return;
            }

            if (map.DisplayName.IndexOf(" by ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            map.DisplayName = map.DisplayName + " (" + addonName + ")";
        }

        private static void ResolveLayerFiles(MapDef map, string absolutePath)
        {
            var root = map.IsAddon ? Path.GetDirectoryName(absolutePath) : Plugin.Path;
            foreach (var layer in map.Layers.Values)
            {
                if (layer == null
                    || string.IsNullOrWhiteSpace(layer.ImagePath)
                    || layer.ImageBounds == null
                    || layer.GameBounds == null)
                {
                    throw new InvalidDataException("Incomplete map layer");
                }

                layer.ImagePath = Path.GetFullPath(Path.Combine(root, layer.ImagePath));
                var extension = Path.GetExtension(layer.ImagePath);
                var supported = extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)
                                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
                if (!supported || !File.Exists(layer.ImagePath))
                {
                    throw new InvalidDataException("Missing or unsupported map image: " + layer.ImagePath);
                }
            }
        }
    }
}
