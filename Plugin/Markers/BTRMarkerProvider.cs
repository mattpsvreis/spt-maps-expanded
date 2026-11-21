using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class BtrMarkerProvider : PinSource
    {
        private const string Icon = "Markers/btr.png";
        private static readonly Vector2 Size = new(45f, 45f);
        private MapMarker _pin;

        protected override void ShowInRaid(MapCanvas map) => Place(map);

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            Lift();
            Place(map);
        }

        protected override void EndRaid(MapCanvas map) => Lift();

        protected override void Disable(MapCanvas map) => Lift();

        protected override void ColorsChanged(MapCanvas map)
        {
            if (_pin != null)
            {
                _pin.Color = Settings.BtrColor.Value;
            }
        }

        private void Place(MapCanvas map)
        {
            if (_pin != null)
            {
                return;
            }

            var btr = GameUtils.GetBtrView();
            if (btr == null)
            {
                return;
            }

            _pin = map.AddTransformMarker(btr.transform, "BTR", "BTR", Settings.BtrColor.Value, Icon, Size);
        }

        private void Lift()
        {
            if (_pin == null)
            {
                return;
            }

            _pin.ContainingMapView?.RemoveMapMarker(_pin);
            _pin = null;
        }
    }
}
