using System.Collections.Generic;
using System.Linq;
using MapsExtended.Presentation.Overlays;
using UnityEngine;

namespace MapsExtended.Markers
{
    internal sealed class PinBag<T>
    {
        private readonly Dictionary<T, MapMarker> _pins = new();

        internal int Count => _pins.Count;
        internal IEnumerable<T> Keys => _pins.Keys;
        internal IEnumerable<MapMarker> Markers => _pins.Values;
        internal bool Has(T key) => _pins.ContainsKey(key);

        internal bool TryGet(T key, out MapMarker marker) => _pins.TryGetValue(key, out marker);

        internal void Put(T key, MapMarker marker)
        {
            _pins[key] = marker;
        }

        internal void Drop(T key)
        {
            if (!_pins.TryGetValue(key, out var marker))
            {
                return;
            }

            marker.ContainingMapView?.RemoveMapMarker(marker);
            _pins.Remove(key);
        }

        internal void Clear()
        {
            foreach (var key in _pins.Keys.ToList())
            {
                Drop(key);
            }
        }

        internal void Paint(Color color)
        {
            foreach (var marker in _pins.Values)
            {
                marker.Color = color;
            }
        }
    }
}
