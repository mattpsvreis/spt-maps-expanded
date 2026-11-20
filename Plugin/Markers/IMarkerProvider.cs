using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;

namespace MapsExtended.Markers
{
    public interface IMarkerProvider
    {
        void OnShowOutOfRaid(MapView map);
        void OnHideOutOfRaid(MapView map);

        void OnShowInRaid(MapView map);
        void OnHideInRaid(MapView map);

        void OnRaidEnd(MapView map);

        void OnMapChanged(MapView map, MapDef mapDef);

        void OnDisable(MapView map);
    }
}
