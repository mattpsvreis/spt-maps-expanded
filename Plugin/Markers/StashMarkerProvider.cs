using System.Linq;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Hooks;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT.Interactive;

namespace MapsExtended.Markers
{
    public class HiddenStashMarkerProvider : PinSource
    {
        private const string Icon = "Markers/barrel.png";
        private readonly PinBag<LootableContainer> _pins = new();

        protected override void ShowInRaid(MapCanvas map)
        {
            foreach (var stash in GameStartedPatch.HiddenStashes)
            {
                Place(stash);
            }
        }

        protected override void EndRaid(MapCanvas map) => _pins.Clear();

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            foreach (var stash in _pins.Keys.ToList())
            {
                _pins.Drop(stash);
                Place(stash);
            }
        }

        protected override void Disable(MapCanvas map) => EndRaid(map);

        public void RefreshMarkers()
        {
            if (!GameUtils.IsInRaid())
            {
                return;
            }

            foreach (var stash in _pins.Keys.ToList())
            {
                _pins.Drop(stash);
                Place(stash);
            }
        }

        private void Place(LootableContainer stash)
        {
            if (Map == null || stash == null || _pins.Has(stash))
            {
                return;
            }

            _pins.Put(stash, Map.AddMapMarker(new MapMarkerDef
            {
                Category = "Hidden Stash",
                Color = Settings.HiddenStashColor.Value,
                ImagePath = Icon,
                Position = MathUtils.ConvertToMapPosition(stash.transform),
                Text = "Hidden Stash"
            }));
        }
    }
}
