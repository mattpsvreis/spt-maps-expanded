using System.Collections.Generic;
using System.Linq;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Hooks;
using MapsExtended.Presentation.Overlays;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using EFT.SynchronizableObjects;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class AirdropMarkerProvider : MarkerProviderBase
    {
        private MapView _lastMapView;
        private readonly Dictionary<AirdropSynchronizableObject, MapMarker> _airdropMarkers = [];

        // TODO: move to config
        private const string AirdropName = "Airdrop";
        private const string AirdropCategory = "Airdrop";
        private const string AirdropImagePath = "Markers/airdrop.png";
        private static readonly Vector2 AirdropPivot = new Vector2(0.5f, 0.25f);
        //

        public override void OnShowInRaid(MapView map)
        {
            _lastMapView = map;
            
            // add all existing airdrops
            foreach (var airdrop in AirdropBoxOnBoxLandPatch.Airdrops)
            {
                TryAddMarker(airdrop);
            }

            // subscribe to new airdrops while map is open
            AirdropBoxOnBoxLandPatch.OnAirdropLanded += TryAddMarker;
        }

        public override void OnHideInRaid(MapView map)
        {
            // unsubscribe to new airdrops, since we're hiding
            AirdropBoxOnBoxLandPatch.OnAirdropLanded -= TryAddMarker;
        }

        public override void OnRaidEnd(MapView map)
        {
            AirdropBoxOnBoxLandPatch.OnAirdropLanded -= TryAddMarker;
            TryRemoveMarkers();
        }

        public override void OnMapChanged(MapView map, MapDef mapDef)
        {
            _lastMapView = map;

            // transition markers from last map to this one
            foreach (var airdrop in _airdropMarkers.Keys.ToList())
            {
                TryRemoveMarker(airdrop);
                TryAddMarker(airdrop);
            }
        }

        public override void OnDisable(MapView map)
        {
            OnRaidEnd(map);
        }

        public void RefreshMarkers()
        {
            if (!GameUtils.IsInRaid()) return;

            foreach (var drop in _airdropMarkers.ToArray())
            {
                TryRemoveMarker(drop.Key);
            }

            foreach (var drop in AirdropBoxOnBoxLandPatch.Airdrops)
            {
                TryAddMarker(drop);
            }
        }
        
        private void TryAddMarker(AirdropSynchronizableObject airdrop)
        {
            if (_airdropMarkers.ContainsKey(airdrop))
            {
                return;
            }

            var markerDef = new MapMarkerDef
            {
                Category = AirdropCategory,
                Color = Settings.AirdropColor.Value,
                ImagePath = AirdropImagePath,
                Position = MathUtils.ConvertToMapPosition(airdrop.transform),
                Pivot = AirdropPivot,
                Text = AirdropName
            };

            var marker = _lastMapView.AddMapMarker(markerDef);
            _airdropMarkers[airdrop] = marker;
        }

        private void TryRemoveMarkers()
        {
            foreach (var airdrop in _airdropMarkers.Keys.ToList())
            {
                TryRemoveMarker(airdrop);
            }
        }

        private void TryRemoveMarker(AirdropSynchronizableObject airdrop)
        {
            if (!_airdropMarkers.ContainsKey(airdrop))
            {
                return;
            }

            _airdropMarkers[airdrop].ContainingMapView.RemoveMapMarker(_airdropMarkers[airdrop]);
            _airdropMarkers.Remove(airdrop);
        }
}
}
