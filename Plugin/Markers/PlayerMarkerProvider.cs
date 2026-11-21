using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;

namespace MapsExtended.Markers
{
    public class PlayerMarkerProvider : PinSource
    {
        private const string Category = "Main Player";
        private const string Icon = "Markers/arrow.png";
        private PlayerMapMarker _pin;

        protected override void ShowInRaid(MapCanvas map) => Place(map);

        protected override void EndRaid(MapCanvas map) => Lift();

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            Lift();
            if (GameUtils.IsInRaid())
            {
                Place(map);
            }
        }

        protected override void Disable(MapCanvas map) => Lift();

        protected override void ColorsChanged(MapCanvas map)
        {
            if (_pin != null)
            {
                _pin.Color = Settings.PlayerColor.Value;
            }
        }

        private void Place(MapCanvas map)
        {
            if (_pin != null)
            {
                return;
            }

            var player = GameUtils.GetMainPlayer();
            if (player == null || player.IsHeadlessClient())
            {
                return;
            }

            _pin = map.AddPlayerMarker(player, Category, Settings.PlayerColor.Value, Icon);
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
