using System.Collections.Generic;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT.Quests;

namespace MapsExtended.Markers
{
    public class QuestMarkerProvider : PinSource
    {
        private readonly List<MapMarker> _pins = [];
        private readonly HashSet<Quest> _watched = [];
        private QuestController _controller;

        protected override void ShowInRaid(MapCanvas map)
        {
            if (GameUtils.IsScavRaid())
            {
                return;
            }

            Watch();
            Refresh();
        }

        protected override void HideInRaid(MapCanvas map)
        {
            Unwatch();
            Clear();
        }

        protected override void ShowOutOfRaid(MapCanvas map) => Refresh();

        protected override void HideOutOfRaid(MapCanvas map) => Clear();

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            if (GameUtils.IsScavRaid())
            {
                Clear();
                return;
            }

            if (GameUtils.IsInRaid())
            {
                Watch();
            }

            Refresh();
        }

        protected override void EndRaid(MapCanvas map)
        {
            Unwatch();
            QuestUtils.DiscardQuestData();
            Clear();
        }

        protected override void Disable(MapCanvas map)
        {
            Unwatch();
            QuestUtils.DiscardQuestData();
            Clear();
        }

        protected override void ColorsChanged(MapCanvas map)
        {
            var color = Settings.QuestColor.Value;
            foreach (var pin in _pins)
            {
                pin.Color = color;
            }
        }

        private void Watch()
        {
            var player = GameUtils.GetMainPlayer();
            if (player == null)
            {
                return;
            }

            var next = player.QuestController;
            if (!ReferenceEquals(_controller, next))
            {
                Unwatch();
                _controller = next;
                if (_controller != null)
                {
                    _controller.OnNewQuestsAdded += OnNew;
                }
            }

            if (_controller?.Quests == null)
            {
                return;
            }

            foreach (var quest in _controller.Quests)
            {
                Watch(quest);
            }
        }

        private void Watch(Quest quest)
        {
            if (quest == null || !_watched.Add(quest))
            {
                return;
            }

            quest.OnConditionChanged += OnChanged;
            quest.OnStatusChanged += OnStatus;
        }

        private void Unwatch()
        {
            if (_controller != null)
            {
                _controller.OnNewQuestsAdded -= OnNew;
                _controller = null;
            }

            foreach (var quest in _watched)
            {
                quest.OnConditionChanged -= OnChanged;
                quest.OnStatusChanged -= OnStatus;
            }

            _watched.Clear();
        }

        private void OnNew(Quest quest)
        {
            Watch(quest);
            Refresh();
        }

        private void OnChanged(Quest quest) => Refresh();

        private void OnStatus(Quest quest, bool notify) => Refresh();

        private void Refresh()
        {
            if (Map == null || !Settings.ShowQuestsInRaid.Value || GameUtils.IsScavRaid())
            {
                Clear();
                return;
            }

            Clear();
            if (GameUtils.IsInRaid())
            {
                PlaceInRaid();
            }
            else
            {
                PlaceOutOfRaid();
            }
        }

        private void PlaceInRaid()
        {
            QuestUtils.TryCaptureQuestData();
            var player = GameUtils.GetMainPlayer();
            if (player == null)
            {
                return;
            }

            var defs = QuestUtils.GetMarkerDefsForPlayer(player);
            if (defs == null)
            {
                return;
            }

            foreach (var def in defs)
            {
                _pins.Add(Map.AddMapMarker(def));
            }
        }

        private void PlaceOutOfRaid()
        {
            var profile = GameUtils.GetPlayerProfile();
            var mapDef = Map.CurrentMapDef;
            if (profile == null || mapDef == null)
            {
                return;
            }

            var defs = QuestUtils.GetMarkerDefsOutOfRaid(profile, mapDef, Map);
            if (defs == null)
            {
                return;
            }

            foreach (var def in defs)
            {
                _pins.Add(Map.AddMapMarker(def));
            }
        }

        private void Clear()
        {
            foreach (var pin in _pins)
            {
                pin.ContainingMapView?.RemoveMapMarker(pin);
            }

            _pins.Clear();
        }
    }
}
