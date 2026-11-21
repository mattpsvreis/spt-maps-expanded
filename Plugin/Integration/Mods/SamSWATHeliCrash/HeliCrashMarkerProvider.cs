using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Markers;
using MapsExtended.Presentation.Overlays;
using EFT;
using UnityEngine;

namespace MapsExtended.Integration.Mods
{
    public class HeliCrashMarkerProvider : PinSource
    {
        private const string ItemId = "6223349b3136504a544d1608";
        private const string Icon = "Markers/helicopter.png";
        private static readonly Vector2 Pivot = new(0.5f, 0.25f);
        private MapMarker _pin;

        protected override void ShowInRaid(MapCanvas map)
        {
            if (_pin != null)
            {
                return;
            }

            Place();
        }

        protected override void EndRaid(MapCanvas map) => Lift();

        protected override void Disable(MapCanvas map) => EndRaid(map);

        protected override void ColorsChanged(MapCanvas map)
        {
            if (_pin != null)
            {
                _pin.Color = Settings.HeliCrashColor.Value;
            }
        }

        private void Place()
        {
            if (Map == null || _pin != null)
            {
                return;
            }

            var found = Singleton<GameWorld>.Instance.FindItemWithWorldData(ItemId);
            var (item, world) = found.Value;
            if (item == null)
            {
                return;
            }

            _pin = Map.AddMapMarker(new MapMarkerDef
            {
                Category = "Airdrop",
                Color = Settings.HeliCrashColor.Value,
                ImagePath = Icon,
                Position = MathUtils.ConvertToMapPosition(world.Transform),
                Pivot = Pivot,
                Text = "Crashed Helicopter"
            });
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
