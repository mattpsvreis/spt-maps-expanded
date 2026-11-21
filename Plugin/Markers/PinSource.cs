using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;

namespace MapsExtended.Markers
{
    public abstract class PinSource
    {
        protected MapCanvas Map { get; private set; }

        internal void Shown(MapCanvas map, bool inRaid)
        {
            Map = map;
            if (inRaid)
            {
                ShowInRaid(map);
            }
            else
            {
                ShowOutOfRaid(map);
            }
        }

        internal void Hidden(MapCanvas map, bool inRaid)
        {
            if (inRaid)
            {
                HideInRaid(map);
            }
            else
            {
                HideOutOfRaid(map);
            }
        }

        internal void RaidEnded(MapCanvas map) => EndRaid(map);

        internal void MapSwapped(MapCanvas map, MapDef mapDef)
        {
            Map = map;
            MapChanged(map, mapDef);
        }

        internal void TurnedOff(MapCanvas map) => Disable(map);

        internal void Recolor(MapCanvas map) => ColorsChanged(map);

        protected virtual void ShowInRaid(MapCanvas map)
        {
        }

        protected virtual void HideInRaid(MapCanvas map)
        {
        }

        protected virtual void ShowOutOfRaid(MapCanvas map)
        {
        }

        protected virtual void HideOutOfRaid(MapCanvas map)
        {
        }

        protected virtual void EndRaid(MapCanvas map)
        {
        }

        protected virtual void MapChanged(MapCanvas map, MapDef mapDef)
        {
        }

        protected virtual void Disable(MapCanvas map)
        {
        }

        protected virtual void ColorsChanged(MapCanvas map)
        {
        }
    }
}
