using System.Collections.Generic;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;
using MapsExtended.Integration;
using EFT.Quests;

namespace MapsExtended.Markers
{
    public class QuestMarkerProvider : MarkerProviderBase
    {
        private readonly List<MapMarker> _questMarkers = [];
        private readonly HashSet<Quest> _subscribedQuests = [];
        private QuestController _questController;
        private MapView _map;

        public override void OnShowInRaid(MapView map)
        {
            _map = map;
            if (GameUtils.IsScavRaid())
            {
                return;
            }

            SubscribeToQuests();
            RefreshMarkers();
        }

        public override void OnHideInRaid(MapView map)
        {
            UnsubscribeFromQuests();
            TryRemoveMarkers();
            _map = null;
        }

        public override void OnShowOutOfRaid(MapView map)
        {
            _map = map;
            RefreshMarkers();
        }

        public override void OnHideOutOfRaid(MapView map)
        {
            TryRemoveMarkers();
            _map = null;
        }

        public override void OnMapChanged(MapView map, MapDef mapDef)
        {
            _map = map;

            if (GameUtils.IsScavRaid())
            {
                TryRemoveMarkers();
                return;
            }

            if (GameUtils.IsInRaid())
            {
                SubscribeToQuests();
            }

            RefreshMarkers();
        }

        public override void OnRaidEnd(MapView map)
        {
            UnsubscribeFromQuests();
            QuestUtils.DiscardQuestData();
            TryRemoveMarkers();
            _map = null;
        }

        public override void OnDisable(MapView map)
        {
            UnsubscribeFromQuests();
            QuestUtils.DiscardQuestData();
            TryRemoveMarkers();
            _map = null;
        }

        public override void OnColorsChanged(MapView map)
        {
            var color = Settings.QuestColor.Value;
            foreach (var marker in _questMarkers)
            {
                marker.Color = color;
            }
        }

        private void SubscribeToQuests()
        {
            var player = GameUtils.GetMainPlayer();
            if (player == null)
            {
                return;
            }

            var controller = player.QuestController;
            if (!ReferenceEquals(_questController, controller))
            {
                UnsubscribeFromQuests();
                _questController = controller;

                if (_questController != null)
                {
                    _questController.OnNewQuestsAdded += OnNewQuest;
                }
            }

            if (_questController?.Quests == null)
                return;

            foreach (var quest in _questController.Quests)
            {
                SubscribeToQuest(quest);
            }
        }

        private void SubscribeToQuest(Quest quest)
        {
            if (quest == null || !_subscribedQuests.Add(quest))
                return;

            quest.OnConditionChanged += OnQuestConditionChanged;
            quest.OnStatusChanged += OnQuestStatusChanged;
        }

        private void UnsubscribeFromQuests()
        {
            if (_questController != null)
            {
                _questController.OnNewQuestsAdded -= OnNewQuest;
                _questController = null;
            }

            foreach (var quest in _subscribedQuests)
            {
                quest.OnConditionChanged -= OnQuestConditionChanged;
                quest.OnStatusChanged -= OnQuestStatusChanged;
            }
            _subscribedQuests.Clear();
        }

        private void OnNewQuest(Quest quest)
        {
            SubscribeToQuest(quest);
            RefreshMarkers();
        }

        private void OnQuestConditionChanged(Quest quest)
        {
            RefreshMarkers();
        }

        private void OnQuestStatusChanged(Quest quest, bool notify)
        {
            RefreshMarkers();
        }

        private void RefreshMarkers()
        {
            if (_map == null || !Settings.ShowQuestsInRaid.Value)
            {
                TryRemoveMarkers();
                return;
            }

            if (GameUtils.IsScavRaid())
            {
                TryRemoveMarkers();
                return;
            }

            TryRemoveMarkers();

            if (GameUtils.IsInRaid())
            {
                AddQuestObjectiveMarkersInRaid(_map);
            }
            else
            {
                AddQuestObjectiveMarkersOutOfRaid(_map);
            }
        }

        private void AddQuestObjectiveMarkersInRaid(MapView map)
        {
            QuestUtils.TryCaptureQuestData();

            var player = GameUtils.GetMainPlayer();
            if (player == null)
                return;

            var markerDefs = QuestUtils.GetMarkerDefsForPlayer(player);
            if (markerDefs == null)
                return;

            foreach (var markerDef in markerDefs)
            {
                var marker = map.AddMapMarker(markerDef);
                _questMarkers.Add(marker);
            }
        }

        private void AddQuestObjectiveMarkersOutOfRaid(MapView map)
        {
            var profile = GameUtils.GetPlayerProfile();
            var mapDef = map.CurrentMapDef;
            if (profile == null || mapDef == null)
                return;

            var markerDefs = QuestUtils.GetMarkerDefsOutOfRaid(profile, mapDef, map);
            if (markerDefs == null)
                return;

            foreach (var markerDef in markerDefs)
            {
                var marker = map.AddMapMarker(markerDef);
                _questMarkers.Add(marker);
            }
        }

        private void TryRemoveMarkers()
        {
            foreach (var marker in _questMarkers)
            {
                marker.ContainingMapView.RemoveMapMarker(marker);
            }
            _questMarkers.Clear();
        }
    }
}
