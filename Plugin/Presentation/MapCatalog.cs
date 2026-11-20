using System;
using System.Collections;
using System.Collections.Generic;
using Comfort.Common;
using MapsExtended.Core;
using MapsExtended.Presentation.Controls;
using MapsExtended.Presentation.Overlays;
using EFT.UI;

namespace MapsExtended.Presentation
{
    /// <summary>
    /// Loads map definitions from Maps/ + Addons, applies location filters / preferences.
    /// </summary>
    internal sealed class MapCatalog
    {
        internal const string MapRelPath = "Maps";

        private MapSelectDropdown _dropdown;
        private MapView _mapView;
        private Action<MapDef> _onMapChosen;

        internal MapSelectDropdown Dropdown => _dropdown;

        internal void Bind(MapSelectDropdown dropdown, MapView mapView, Action<MapDef> onMapChosen)
        {
            _dropdown = dropdown;
            _mapView = mapView;
            _onMapChosen = onMapChosen;
            _dropdown.OnMapSelected += def => _onMapChosen?.Invoke(def);
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
            if (_mapView.CurrentMapDef == null)
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
            Singleton<CommonUI>.Instance.StartCoroutine(PrecacheCoroutine(GetMapDefs()));
        }

        private static IEnumerator PrecacheCoroutine(IEnumerable<MapDef> mapDefs)
        {
            foreach (var mapDef in mapDefs)
            {
                if (mapDef.IsAddon)
                {
                    continue;
                }

                foreach (var layerDef in mapDef.Layers.Values)
                {
                    Plugin.Log?.LogInfo($"Precaching sprite: {layerDef.ImagePath}");
                    if (!System.IO.Path.GetExtension(layerDef.ImagePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        SvgUtils.GetOrLoadCachedSprite(layerDef);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log?.LogError($"Failed to precache {layerDef.ImagePath}: {e}");
                    }

                    yield return null;
                }
            }
        }
    }
}
