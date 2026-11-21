using System.Collections.Generic;
using System.Linq;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Hooks;
using MapsExtended.Presentation.Overlays;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using EFT.Interactive;

namespace MapsExtended.Markers;

public class HiddenStashMarkerProvider : MarkerProviderBase
{
    private MapView _lastMapView;
    private readonly Dictionary<LootableContainer, MapMarker> _stashMarkers = [];
    private const string HiddenCacheImagePath = "Markers/barrel.png";
    
    public override void OnShowInRaid(MapView map)
    {
        _lastMapView = map;

        foreach (var stash in GameStartedPatch.HiddenStashes)
        {
            TryAddMarker(stash);
        }
    }
public override void OnRaidEnd(MapView map)
    {
        TryRemoveMarkers();
    }

    public override void OnMapChanged(MapView map, MapDef mapDef)
    {
        _lastMapView = map;

        foreach (var stash in _stashMarkers.Keys.ToList())
        {
            TryRemoveMarker(stash);
            TryAddMarker(stash);
        }
    }

    public override void OnDisable(MapView map)
    {
        OnRaidEnd(map);
    }

    public void RefreshMarkers()
    {
        if (!GameUtils.IsInRaid()) return;

        foreach (var stash in _stashMarkers.Keys.ToList())
        {
            TryRemoveMarker(stash);
            TryAddMarker(stash);
        }
    }

    private void TryAddMarker(LootableContainer stash)
    {
        if (_stashMarkers.ContainsKey(stash)) 
            return;
        
        var markerDef = new MapMarkerDef
        {
            Category = "Hidden Stash",
            Color = Settings.HiddenStashColor.Value,
            ImagePath = HiddenCacheImagePath,
            Position = MathUtils.ConvertToMapPosition(stash.transform),
            Text = "Hidden Stash"
        };

        var marker = _lastMapView.AddMapMarker(markerDef);
        _stashMarkers[stash] = marker;
    }

    private void TryRemoveMarkers()
    {
        foreach (var stash in _stashMarkers.Keys.ToList())
        {
            TryRemoveMarker(stash);
        }
    }

    private void TryRemoveMarker(LootableContainer stash)
    {
        if (!_stashMarkers.ContainsKey(stash)) return;
        
        _stashMarkers[stash].ContainingMapView.RemoveMapMarker(_stashMarkers[stash]);
        _stashMarkers.Remove(stash);
    }
}
