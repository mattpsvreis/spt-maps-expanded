using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class BtrMarkerProvider : MarkerProviderBase
    {
        // TODO: move to config
        private static readonly string BtrIconPath = "Markers/btr.png";
        private static readonly Vector2 BtrSize = new(45, 45f);
        private static readonly string BtrName = "BTR";
        private static readonly string BtrCategory = "BTR";
        //

        private MapMarker _btrMarker;

        public override void OnShowInRaid(MapView map)
        {
            if (_btrMarker is not null)
            {
                return;
            }

            TryAddMarker(map);
        }

        public override void OnMapChanged(MapView map, MapDef mapDef)
        {
            TryRemoveMarker();
            TryAddMarker(map);
        }

        public override void OnRaidEnd(MapView map)
        {
            TryRemoveMarker();
        }

        public override void OnDisable(MapView map)
        {
            TryRemoveMarker();
        }

        private void TryAddMarker(MapView map)
        {
            if (_btrMarker != null)
            {
                return;
            }

            var btrView = GameUtils.GetBtrView();
            if (btrView is null)
            {
                return;
            }

            var color = Settings.BtrColor.Value;
            
            // try adding the marker
            _btrMarker = map.AddTransformMarker(btrView.transform, BtrName, BtrCategory, color,
                                                BtrIconPath, BtrSize);
        }

        private void TryRemoveMarker()
        {
            if (_btrMarker is null)
            {
                return;
            }

            _btrMarker.ContainingMapView.RemoveMapMarker(_btrMarker);
            _btrMarker = null;
        }
}
}
