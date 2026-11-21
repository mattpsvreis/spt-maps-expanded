using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;

namespace MapsExtended.Markers
{
    /// <summary>
    /// Shared no-op lifecycle defaults for marker providers.
    /// </summary>
    public abstract class MarkerProviderBase : IMarkerProvider
    {
        public virtual void OnShowOutOfRaid(MapView map)
        {
        }

        public virtual void OnHideOutOfRaid(MapView map)
        {
        }

        public virtual void OnShowInRaid(MapView map)
        {
        }

        public virtual void OnHideInRaid(MapView map)
        {
        }

        public virtual void OnRaidEnd(MapView map)
        {
        }

        public virtual void OnMapChanged(MapView map, MapDef mapDef)
        {
        }

        public virtual void OnDisable(MapView map)
        {
        }

        public virtual void OnColorsChanged(MapView map)
        {
        }
    }
}
