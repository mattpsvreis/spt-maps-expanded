using System.Linq;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Hooks;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT.SynchronizableObjects;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class AirdropMarkerProvider : PinSource
    {
        private const string Icon = "Markers/airdrop.png";
        private static readonly Vector2 Pivot = new(0.5f, 0.25f);
        private readonly PinBag<AirdropSynchronizableObject> _pins = new();

        protected override void ShowInRaid(MapCanvas map)
        {
            foreach (var drop in AirdropBoxOnBoxLandPatch.Airdrops)
            {
                Place(drop);
            }

            AirdropBoxOnBoxLandPatch.OnAirdropLanded += Place;
        }

        protected override void HideInRaid(MapCanvas map)
        {
            AirdropBoxOnBoxLandPatch.OnAirdropLanded -= Place;
        }

        protected override void EndRaid(MapCanvas map)
        {
            AirdropBoxOnBoxLandPatch.OnAirdropLanded -= Place;
            _pins.Clear();
        }

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            foreach (var drop in _pins.Keys.ToList())
            {
                _pins.Drop(drop);
                Place(drop);
            }
        }

        protected override void Disable(MapCanvas map) => EndRaid(map);

        public void RefreshMarkers()
        {
            if (!GameUtils.IsInRaid())
            {
                return;
            }

            _pins.Clear();
            foreach (var drop in AirdropBoxOnBoxLandPatch.Airdrops)
            {
                Place(drop);
            }
        }

        private void Place(AirdropSynchronizableObject drop)
        {
            if (Map == null || drop == null || _pins.Has(drop))
            {
                return;
            }

            _pins.Put(drop, Map.AddMapMarker(new MapMarkerDef
            {
                Category = "Airdrop",
                Color = Settings.AirdropColor.Value,
                ImagePath = Icon,
                Position = MathUtils.ConvertToMapPosition(drop.transform),
                Pivot = Pivot,
                Text = "Airdrop"
            }));
        }
    }
}
