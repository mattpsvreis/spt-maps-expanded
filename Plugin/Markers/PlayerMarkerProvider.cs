using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class PlayerMarkerProvider : MarkerProviderBase
    {
        // TODO: move to config
        private const string PlayerCategory = "Main Player";
        private const string PlayerImagePath = "Markers/arrow.png";
        //

        private PlayerMapMarker _playerMarker;

        public override void OnShowInRaid(MapView map)
        {
            TryAddMarker(map);
        }

        public override void OnRaidEnd(MapView map)
        {
            TryRemoveMarker();
        }

        public override void OnMapChanged(MapView map, MapDef mapDef)
        {
            TryRemoveMarker();

            if (GameUtils.IsInRaid())
            {
                TryAddMarker(map);
            }
        }

        public override void OnDisable(MapView map)
        {
            TryRemoveMarker();
        }

        public override void OnColorsChanged(MapView map)
        {
            if (_playerMarker != null)
            {
                _playerMarker.Color = Settings.PlayerColor.Value;
            }
        }

        private void TryAddMarker(MapView map)
        {
            if (_playerMarker is not null)
            {
                return;
            }

            var player = GameUtils.GetMainPlayer();
            if (player is null || player.IsHeadlessClient())
            {
                return;
            }

            var color = Settings.PlayerColor.Value;
            
            // try adding the marker
            _playerMarker = map.AddPlayerMarker(player, PlayerCategory, color, PlayerImagePath);
        }

        private void TryRemoveMarker()
        {
            if (_playerMarker is null)
            {
                return;
            }

            _playerMarker.ContainingMapView.RemoveMapMarker(_playerMarker);
            _playerMarker = null;
        }
    }
}
