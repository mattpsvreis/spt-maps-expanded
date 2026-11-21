using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI;
using HarmonyLib;
using MapsExtended.App;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using UnityEngine;

namespace MapsExtended.Presentation.Controls
{
    public class MapSelectDropdown : MonoBehaviour
    {
        public event Action<MapDef> OnMapSelected;

        public RectTransform RectTransform => transform as RectTransform;

        private DropDownBox _box;
        private readonly Dictionary<string, MapDef> _loaded = new();
        private List<MapDef> _shown;
        private string _locationFilter;
        private static MethodInfo _quietUpdate;

        public static MapSelectDropdown Create(GameObject prefab, Transform parent)
        {
            var go = Instantiate(prefab);
            go.name = "MapSelectDropdown";

            var rect = go.GetRectTransform();
            rect.SetParent(parent);
            rect.localScale = Vector3.one;
            return go.AddComponent<MapSelectDropdown>();
        }

        private void Awake()
        {
            _box = GetComponentInChildren<DropDownBox>();
            _box.SetLabelText("Select a Map");
            _box.OnValueChanged.Subscribe(Pick);
        }

        private void Pick(int index)
        {
            if (_shown == null || index < 0 || index >= _shown.Count)
            {
                return;
            }

            OnMapSelected?.Invoke(_shown[index]);
        }

        private void Publish(IEnumerable<MapDef> maps)
        {
            var next = maps as List<MapDef> ?? maps.ToList();
            if (_shown != null && next.SequenceEqual(_shown))
            {
                return;
            }

            _shown = next;
            if (_shown.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            _box.Show(_shown.Select(def => def.DisplayName));
            gameObject.SetActive(_shown.Count > 1);
        }

        public void LoadMapDefsFromPath(string relPath)
        {
            _ = relPath;
            MapLibrary.Refresh(Plugin.Path);
            _loaded.Clear();
            foreach (var pair in MapLibrary.Current)
            {
                _loaded[pair.Key] = pair.Value;
            }

            RememberPreferences();
            Publish(VisibleMaps());
        }

        private Dictionary<string, MapDef> ChoicesFor(string location)
        {
            var matches = _loaded.Values
                .Where(map => map.MapInternalNames.Contains(location, StringComparer.OrdinalIgnoreCase))
                .OrderBy(map => map.IsAddon)
                .ThenBy(map => map.SelectionId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new Dictionary<string, MapDef>(StringComparer.Ordinal);
            foreach (var map in matches)
            {
                var sameLabel = 0;
                foreach (var other in matches)
                {
                    if (string.Equals(other.DisplayName, map.DisplayName, StringComparison.Ordinal))
                    {
                        sameLabel++;
                    }
                }

                var label = sameLabel == 1 ? map.DisplayName : $"{map.DisplayName} [{map.SelectionId}]";
                result[label] = map;
            }

            return result;
        }

        private void RememberPreferences()
        {
            var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var map in _loaded.Values)
            {
                foreach (var name in map.MapInternalNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        locations.Add(name);
                    }
                }
            }

            foreach (var location in locations)
            {
                Settings.RegisterMapPreference(location, ChoicesFor(location).Keys);
            }
        }

        public void RemoveUnavailableMap(MapDef mapDef)
        {
            var drop = new List<string>();
            foreach (var pair in _loaded)
            {
                if (pair.Value == mapDef)
                {
                    drop.Add(pair.Key);
                }
            }

            foreach (var key in drop)
            {
                _loaded.Remove(key);
            }

            Publish(VisibleMaps());
        }

        public bool HasMapForLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            var alias = MapAlias.Resolve(location);
            foreach (var map in _loaded.Values)
            {
                if (map.MapInternalNames.Contains(location, StringComparer.OrdinalIgnoreCase)
                    || map.MapInternalNames.Contains(alias, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void LoadPreferredMap(string location)
        {
            location = MapAlias.Resolve(location);
            var choices = ChoicesFor(location);
            if (choices.Count == 0)
            {
                return;
            }

            if (choices.TryGetValue(Settings.GetPreferredMap(location), out var preferred) && _loaded.ContainsValue(preferred))
            {
                OnMapSelected?.Invoke(preferred);
                return;
            }

            foreach (var fallback in choices.Values)
            {
                if (_loaded.ContainsValue(fallback))
                {
                    OnMapSelected?.Invoke(fallback);
                    return;
                }
            }
        }

        private IEnumerable<MapDef> VisibleMaps()
        {
            var list = new List<MapDef>();
            foreach (var map in _loaded.Values)
            {
                if (string.IsNullOrEmpty(_locationFilter)
                    || map.MapInternalNames.Contains(_locationFilter, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(map);
                }
            }

            list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
            return list;
        }

        public IEnumerable<MapDef> GetMapDefs() => _loaded.Values.ToArray();

        public void LoadFirstAvailableMap()
        {
            if (_shown == null || _shown.Count == 0)
            {
                return;
            }

            Pick(0);
        }

        public void FilterByInternalMapName(string internalMapName)
        {
            _locationFilter = MapAlias.Resolve(internalMapName);
            Publish(VisibleMaps());
        }

        public void ClearFilter()
        {
            _locationFilter = null;
            Publish(VisibleMaps());
        }

        public void OnLoadMap(MapDef mapDef)
        {
            if (_box == null || _shown == null || mapDef == null)
            {
                return;
            }

            var index = _shown.IndexOf(mapDef);
            if (index < 0)
            {
                return;
            }

            // Update the label without the click sound or a second OnValueChanged.
            _quietUpdate ??= AccessTools.Method(
                typeof(BaseDropDownBox),
                "UpdateValue",
                new[] { typeof(int), typeof(bool), typeof(int?), typeof(int?) });

            if (_quietUpdate != null)
            {
                _quietUpdate.Invoke(_box, new object[] { index, false, null, null });
            }
            else
            {
                _box.UpdateValue(index);
            }
        }

        public bool IsDropdownOpen() => _box.CurrentState;

        public void TryCloseDropdown() => _box.Close();
    }
}
