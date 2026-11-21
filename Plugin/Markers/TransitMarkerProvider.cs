using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Integration;
using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;
using EFT;
using EFT.Interactive;

namespace MapsExtended.Markers
{
    public class TransitMarkerProvider : PinSource
    {
        private const string Icon = "Markers/transit.png";
        private readonly PinBag<TransitPoint> _pins = new();

        protected override void ShowInRaid(MapCanvas map)
        {
            if (_pins.Count > 0)
            {
                return;
            }

            var controller = Singleton<GameWorld>.Instance.TransitController;
            if (controller == null)
            {
                return;
            }

            foreach (var point in controller.pointsById.Values)
            {
                Place(point);
            }
        }

        protected override void EndRaid(MapCanvas map) => _pins.Clear();

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            foreach (var point in _pins.Keys.ToList())
            {
                _pins.Drop(point);
                Place(point);
            }
        }

        protected override void Disable(MapCanvas map) => _pins.Clear();

        public void RefreshMarkers(MapCanvas map)
        {
            foreach (var point in _pins.Keys.ToList())
            {
                _pins.Drop(point);
                Place(point);
            }
        }

        private void Place(TransitPoint point)
        {
            if (Map == null || point == null || _pins.Has(point))
            {
                return;
            }

            _pins.Put(point, Map.AddMapMarker(new MapMarkerDef
            {
                Category = "Transit",
                ImagePath = Icon,
                Text = point.parameters.description.BsgLocalized(),
                Position = MathUtils.ConvertToMapPosition(point.transform),
                Color = Settings.TransPointColor.Value
            }));
        }
    }
}
