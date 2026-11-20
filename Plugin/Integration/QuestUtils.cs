using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Comfort.Common;
using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;
using EFT;
using EFT.Interactive;
using EFT.Quests;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace MapsExtended.Integration
{
    // NOTE: Most of this is adapted from work done for Prop's GTFO mod (https://github.com/dvize/GTFO)
    // this likely does not count as a "substantial portion" of the software
    // under MIT license (https://github.com/dvize/GTFO/blob/master/LICENSE.txt)
    public static class QuestUtils
    {
        // TODO: move to config
        private const string QuestCategory = "Quest";
        private const string QuestImagePath = "Markers/quest.png";
        private static readonly Vector2 QuestPivot = new Vector2(0.5f, 0f);
        private static readonly Color QuestColor = Color.green;
        private static readonly string ZoneCachePath = Path.Combine(Plugin.Path, "Cache", "quest_zones.json");

        private static List<TriggerWithId> _triggersWithIds;
        private static List<LootItem> _questItems;
        private static Dictionary<string, Dictionary<string, List<float[]>>> _zoneCache;
        private static bool _zoneCacheLoaded;

        // Optional out-of-raid context (current map's locations + extract markers).
        private static IReadOnlyList<string> _oorLocations;
        private static MapView _oorMapView;

        internal static void TryCaptureQuestData()
        {
            var gameWorld = Singleton<GameWorld>.Instance;

            if (_triggersWithIds == null)
            {
                _triggersWithIds = GameObject.FindObjectsOfType<TriggerWithId>().ToList();
                PersistZoneCacheForCurrentLocation();
            }

            if (_questItems == null)
            {
                _questItems = Traverse.Create(gameWorld)
                    .Field("LootItems")
                    .Field("_iteration")
                    .GetValue<List<LootItem>>()
                    .Where(i => i.Item.QuestItem).ToList();
            }
        }

        internal static void DiscardQuestData()
        {
            if (_triggersWithIds != null)
            {
                _triggersWithIds.Clear();
                _triggersWithIds = null;
            }

            if (_questItems != null)
            {
                _questItems.Clear();
                _questItems = null;
            }
        }

        internal static IEnumerable<MapMarkerDef> GetMarkerDefsForPlayer(Player player)
        {
            if (_triggersWithIds == null || _questItems == null)
            {
                Plugin.Log.LogWarning($"TriggersWithIds null: {_triggersWithIds == null} or QuestItems null: {_questItems == null}");
                return null;
            }

            var markers = new List<MapMarkerDef>();

            var quests = GetIncompleteQuests(player);
            foreach (var quest in quests)
            {
                markers.AddRange(GetMarkerDefsForQuest(player, quest));
            }

            return markers;
        }

        /// <summary>
        /// Out-of-raid markers use profile quest progress + zone positions cached from prior raids.
        /// Visit a map in raid once so its quest zones are recorded.
        /// </summary>
        internal static IEnumerable<MapMarkerDef> GetMarkerDefsOutOfRaid(Profile profile, MapDef mapDef, MapView mapView)
        {
            if (profile?.QuestsData == null || mapDef == null)
            {
                return Array.Empty<MapMarkerDef>();
            }

            EnsureZoneCacheLoaded();
            _oorLocations = mapDef.MapInternalNames ?? [];
            _oorMapView = mapView;

            try
            {
                var markers = new List<MapMarkerDef>();
                foreach (var quest in GetIncompleteQuestsFromProfile(profile))
                {
                    markers.AddRange(GetMarkerDefsForQuest(null, quest));
                }

                return markers;
            }
            finally
            {
                _oorLocations = null;
                _oorMapView = null;
            }
        }

        private static IEnumerable<MapMarkerDef> GetMarkerDefsForQuest(Player player, QuestDataClass quest)
        {
            var markers = new List<MapMarkerDef>();

            var conditions = GetIncompleteQuestConditions(player, quest);
            foreach (var condition in conditions)
            {
                var questName = quest.Template.NameLocaleKey.BsgLocalized();
                var conditionDescription = condition.id.BsgLocalized();

                var positions = GetPositionsForCondition(condition, questName, conditionDescription);
                foreach (var position in positions)
                {
                    var isDuplicate = false;

                    // check against previously created markers for duplicate position
                    foreach (var marker in markers)
                    {
                        if (MathUtils.ApproxEquals(marker.Position.x, position.x)
                         && MathUtils.ApproxEquals(marker.Position.y, position.y)
                         && MathUtils.ApproxEquals(marker.Position.z, position.z))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (isDuplicate)
                    {
                        continue;
                    }

                    markers.Add(CreateQuestMapMarkerDef(position, questName, conditionDescription));
                }
            }

            return markers;
        }

        private static IEnumerable<Vector3> GetPositionsForCondition(Condition condition, string questName,
                                                                    string conditionDescription)
        {
            switch (condition)
            {
                case ConditionZone zoneCondition:
                {
                    foreach (var position in GetPositionsForZoneId(zoneCondition.zoneId, questName, conditionDescription))
                    {
                        yield return position;
                    }
                    break;
                }
                case ConditionLaunchFlare flareCondition:
                {
                    foreach (var position in GetPositionsForZoneId(flareCondition.zoneID, questName, conditionDescription))
                    {
                        yield return position;
                    }
                    break;
                }
                case ConditionVisitPlace place:
                {
                    foreach (var position in GetPositionsForZoneId(place.target, questName, conditionDescription))
                    {
                        yield return position;
                    }
                    break;
                }
                case ConditionInZone zone:
                {
                    foreach (var zoneId in zone.zoneIds)
                    {
                        foreach (var position in GetPositionsForZoneId(zoneId, questName, conditionDescription))
                        {
                            yield return position;
                        }
                    }
                    break;
                }
                case ConditionFindItem findItemCondition:
                {
                    foreach (var position in GetPositionsForQuestItems(findItemCondition.target, questName, conditionDescription))
                    {
                        yield return position;
                    }
                    break;
                }
                case ConditionExitName exitCondition:
                {
                    foreach (var position in GetPositionsForExit(exitCondition.exitName))
                    {
                        yield return position;
                    }

                    break;
                }
                case ConditionCounterCreator conditionCreator:
                {
                    // this will recurse back into this method
                    foreach (var position in GetPositionsForConditionCreator(conditionCreator, questName, conditionDescription))
                    {
                        yield return position;
                    }
                    break;
                }
            }
        }

        private static IEnumerable<Vector3> GetPositionsForConditionCreator(ConditionCounterCreator conditionCreator,
                                                                            string questName, string conditionDescription)
        {
            var counter = conditionCreator._templateConditions;
            foreach (var condition in counter.Conditions)
            {
                foreach (var position in GetPositionsForCondition(condition, questName, conditionDescription))
                {
                    yield return position;
                }
            }
        }

        private static IEnumerable<Vector3> GetPositionsForZoneId(string zoneId, string questName,
                                                                  string conditionDescription)
        {
            if (_triggersWithIds != null)
            {
                var zones = _triggersWithIds.GetZoneTriggers(zoneId);
                foreach (var zone in zones)
                {
                    yield return MathUtils.ConvertToMapPosition(zone.transform.position);
                }

                yield break;
            }

            // Out of raid: use positions cached from previous raids on this map.
            EnsureZoneCacheLoaded();
            if (_zoneCache == null || _oorLocations == null)
            {
                yield break;
            }

            foreach (var location in _oorLocations)
            {
                if (!_zoneCache.TryGetValue(location, out var zonesById))
                {
                    continue;
                }

                if (!zonesById.TryGetValue(zoneId, out var positions))
                {
                    continue;
                }

                foreach (var raw in positions)
                {
                    if (raw == null || raw.Length < 3)
                    {
                        continue;
                    }

                    yield return new Vector3(raw[0], raw[1], raw[2]);
                }
            }
        }

        private static IEnumerable<Vector3> GetPositionsForExit(string exitName)
        {
            if (string.IsNullOrEmpty(exitName))
            {
                yield break;
            }

            if (GameUtils.IsInRaid() && Singleton<GameWorld>.Instance?.ExfiltrationController?.ExfiltrationPoints != null)
            {
                var specifiedExit = Singleton<GameWorld>.Instance.ExfiltrationController.ExfiltrationPoints
                    .FirstOrDefault(e => e.Settings.Name == exitName);

                if (specifiedExit != null)
                {
                    yield return MathUtils.ConvertToMapPosition(specifiedExit.transform);
                }

                yield break;
            }

            // Out of raid: match static extract markers already on the map.
            if (_oorMapView == null)
            {
                yield break;
            }

            foreach (var marker in _oorMapView.GetMapMarkersByCategory("Extract"))
            {
                if (string.Equals(marker.Text, exitName, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrEmpty(marker.Text) && marker.Text.IndexOf(exitName, StringComparison.OrdinalIgnoreCase) >= 0)
                    || exitName.IndexOf(marker.Text ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return marker.Position;
                }
            }
        }

        private static IEnumerable<Vector3> GetPositionsForQuestItems(IEnumerable<string> questItemIds, string questName,
                                                                      string conditionDescription)
        {
            // Quest item pickups only exist in the live raid scene.
            if (_questItems == null || questItemIds == null)
            {
                yield break;
            }

            foreach (var questItemId in questItemIds)
            {
                var questItems = _questItems.Where(i => i.TemplateId == questItemId);
                foreach (var item in questItems)
                {
                    yield return MathUtils.ConvertToMapPosition(item.transform.position);
                }
            }
        }

        private static IEnumerable<Condition> GetIncompleteQuestConditions(Player player, QuestDataClass quest)
        {
            // TODO: Template.Conditions is a GClass reference
            if (quest?.Template?.Conditions == null)
            {
                Plugin.Log.LogError($"GetIncompleteQuestConditions: quest.Template.Conditions is null, skipping quest");
                yield break;
            }

            // TODO: conditions is a GClass reference
            if (!quest.Template.Conditions.TryGetValue(EQuestStatus.AvailableForFinish, out var conditions) || conditions == null)
            {
                Plugin.Log.LogError($"Quest {quest.Template.NameLocaleKey.BsgLocalized()} doesn't have conditions marked AvailableForFinish, skipping it");
                yield break;
            }

            foreach (var condition in conditions)
            {
                if (condition == null)
                {
                    Plugin.Log.LogWarning($"Quest {quest.Template.NameLocaleKey.BsgLocalized()} has null condition, skipping it");
                    continue;
                }

                // filter out completed conditions
                if (IsConditionCompleted(player, quest, condition))
                {
                    continue;
                }

                yield return condition;
            }
        }

        private static IEnumerable<QuestDataClass> GetIncompleteQuests(Player player)
        {
            var questController = player.QuestController;
            if (questController == null)
            {
                Plugin.Log.LogError($"Not able to get quests for player: {player.Id}, questController is null");
                yield break;
            }

            var quests = questController.Quests;
            if (quests == null)
            {
                Plugin.Log.LogError($"Not able to get quests for player: {player.Id}, quests is null");
                yield break;
            }

            var questsList = quests.list_1;
            if (questsList == null)
            {
                Plugin.Log.LogError($"Not able to get quests for player: {player.Id}, questsList is null");
                yield break;
            }

            foreach (var quest in questsList)
            {
                if (quest?.Template?.Conditions == null)
                {
                    continue;
                }

                if (quest.Status != EQuestStatus.Started)
                {
                    continue;
                }

                yield return quest;
            }
        }

        private static IEnumerable<QuestDataClass> GetIncompleteQuestsFromProfile(Profile profile)
        {
            if (profile?.QuestsData == null)
            {
                yield break;
            }

            foreach (var quest in profile.QuestsData)
            {
                if (quest?.Template?.Conditions == null)
                {
                    continue;
                }

                if (quest.Status != EQuestStatus.Started)
                {
                    continue;
                }

                yield return quest;
            }
        }

        private static bool IsConditionCompleted(Player player, QuestDataClass questData, Condition condition)
        {
            // CompletedConditions is inaccurate (it doesn't reset when some quests do on death)
            // and also does not contain optional objectives, need to recheck if something is in there
            if (condition.IsNecessary && !questData.CompletedConditions.Contains(condition.id))
            {
                return false;
            }

            // Out of raid (or no live quest controller): fall back to CompletedConditions only.
            if (player?.QuestController == null)
            {
                return questData.CompletedConditions != null
                       && questData.CompletedConditions.Contains(condition.id);
            }

            var quests = player.QuestController.Quests;
            if (quests == null)
            {
                return false;
            }

            var quest = quests.GetConditional(questData.Id);
            if (quest == null)
            {
                return false;
            }

            return quest.IsConditionDone(condition);
        }

        private static void PersistZoneCacheForCurrentLocation()
        {
            if (_triggersWithIds == null || _triggersWithIds.Count == 0)
            {
                return;
            }

            var location = GameUtils.GetCurrentMapInternalName();
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            EnsureZoneCacheLoaded();
            _zoneCache ??= new Dictionary<string, Dictionary<string, List<float[]>>>(StringComparer.OrdinalIgnoreCase);

            var zones = new Dictionary<string, List<float[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (var trigger in _triggersWithIds)
            {
                if (trigger == null || string.IsNullOrWhiteSpace(trigger.Id))
                {
                    continue;
                }

                var mapPos = MathUtils.ConvertToMapPosition(trigger.transform.position);
                if (!zones.TryGetValue(trigger.Id, out var list))
                {
                    list = [];
                    zones[trigger.Id] = list;
                }

                list.Add([mapPos.x, mapPos.y, mapPos.z]);
            }

            _zoneCache[location] = zones;

            try
            {
                var dir = Path.GetDirectoryName(ZoneCachePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(ZoneCachePath, JsonConvert.SerializeObject(_zoneCache));
                Plugin.DebugLog($"Cached {_triggersWithIds.Count} quest zone triggers for {location}");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"Failed to persist quest zone cache: {e.Message}");
            }
        }

        private static void EnsureZoneCacheLoaded()
        {
            if (_zoneCacheLoaded)
            {
                return;
            }

            _zoneCacheLoaded = true;
            try
            {
                if (File.Exists(ZoneCachePath))
                {
                    _zoneCache = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<float[]>>>>(
                        File.ReadAllText(ZoneCachePath));
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"Failed to load quest zone cache: {e.Message}");
                _zoneCache = null;
            }

            _zoneCache ??= new Dictionary<string, Dictionary<string, List<float[]>>>(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<TriggerWithId> GetZoneTriggers(this IEnumerable<TriggerWithId> triggerWithIds, string zoneId)
        {
            return triggerWithIds.Where(t => t.Id == zoneId);
        }

        private static MapMarkerDef CreateQuestMapMarkerDef(Vector3 position, string questName, string conditionDescription)
        {
            return new MapMarkerDef
            {
                Category = QuestCategory,
                Color = QuestColor,
                ImagePath = QuestImagePath,
                Position = position,
                Pivot = QuestPivot,
                Text = questName
            };
        }
    }
}
