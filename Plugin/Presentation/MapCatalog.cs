using System;
using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using EFT.UI;
using MapsExtended.Core;
using MapsExtended.Presentation.Controls;
using MapsExtended.Presentation.Overlays;

namespace MapsExtended.Presentation
{
    /// <summary>
    /// Hands the dropdown the maps discovered under Maps/ and Addons, then
    /// narrows that list to the raid location or the player's saved pack.
    /// </summary>
    internal sealed class MapCatalog
    {
        internal const string MapRelPath = "Maps";

        private MapSelectDropdown _dropdown;
        private MapCanvas _canvas;
        private Action<MapDef> _chosen;

        internal MapSelectDropdown Dropdown => _dropdown;

        internal void Bind(MapSelectDropdown dropdown, MapCanvas mapView, Action<MapDef> onMapChosen)
        {
            _dropdown = dropdown;
            _canvas = mapView;
            _chosen = onMapChosen;
            _dropdown.OnMapSelected += def => _chosen?.Invoke(def);
        }

        internal void LoadAll() => _dropdown.LoadMapDefsFromPath(MapRelPath);

        internal void ApplyRaidLocation(string location)
        {
            _dropdown.FilterByInternalMapName(location);
            _dropdown.LoadPreferredMap(location);
        }

        internal void ApplyOutOfRaid()
        {
            _dropdown.ClearFilter();
            if (_canvas.CurrentMapDef == null)
            {
                _dropdown.LoadFirstAvailableMap();
            }
        }

        internal bool HasMapForLocation(string location) => _dropdown.HasMapForLocation(location);

        internal void NotifyLoaded(MapDef mapDef) => _dropdown.OnLoadMap(mapDef);

        internal void RemoveUnavailable(MapDef mapDef) => _dropdown.RemoveUnavailableMap(mapDef);

        internal void TryClose() => _dropdown?.TryCloseDropdown();

        internal IEnumerable<MapDef> GetMapDefs() => _dropdown.GetMapDefs();

        internal bool IsDropdownOpen() => _dropdown.isActiveAndEnabled && _dropdown.IsDropdownOpen();

        internal void PrecacheBundledLayers()
        {
            Singleton<CommonUI>.Instance.StartCoroutine(WarmSprites(GetMapDefs()));
        }

        private static IEnumerator WarmSprites(IEnumerable<MapDef> maps)
        {
            foreach (var map in maps)
            {
                if (map.IsAddon)
                {
                    continue;
                }

                foreach (var layer in map.Layers.Values)
                {
                    Plugin.Log?.LogInfo($"Precaching sprite: {layer.ImagePath}");
                    if (!layer.ImagePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        SvgUtils.GetOrLoadCachedSprite(layer);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogError($"Failed to precache {layer.ImagePath}: {ex}");
                    }

                    yield return null;
                }
            }
        }
    }
}
