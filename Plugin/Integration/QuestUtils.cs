using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Quests;
using HarmonyLib;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;
using Newtonsoft.Json;
using UnityEngine;

namespace MapsExtended.Integration
{
    /// <summary>
    /// Quest pins for the current raid and for the hideout map.
    /// In raid, zone triggers and quest-item loot are captured from the live scene.
    /// Out of raid, zone positions come from a cache written the last time that map was played.
    /// </summary>
    public static class QuestUtils
    {
        private const string Category = "Quest";
        private const string ImagePath = "Markers/quest.png";
        private const string ExtractCategory = "Extract";
        private const float VisitHintRadius = 25f;
        private const float SpawnDedupRadius = 1f;

        private static readonly Vector2 Pivot = new(0.5f, 0f);
        private static readonly string ZoneCachePath = Path.Combine(Plugin.Path, "Cache", "quest_zones.json");

        private static List<TriggerWithId> _triggers;
        private static List<LootItem> _questItems;
        private static readonly Dictionary<string, List<Vector3>> _itemSpawns = new(StringComparer.Ordinal);
        private static Dictionary<string, Dictionary<string, List<float[]>>> _zoneCache;
        private static bool _zoneCacheLoaded;

        private static IReadOnlyList<string> _outOfRaidLocations;
        private static MapCanvas _outOfRaidMap;

        internal static void TryCaptureQuestData()
        {
            var world = Singleton<GameWorld>.Instance;

            if (_triggers == null)
            {
                _triggers = GameObject.FindObjectsOfType<TriggerWithId>().ToList();
                SaveZonesForCurrentLocation();
            }

            if (_questItems == null)
            {
                _questItems = Traverse.Create(world)
                    .Field("LootItems")
                    .Field("_iteration")
                    .GetValue<List<LootItem>>()
                    .Where(item => item.Item.QuestItem)
                    .ToList();
            }

            RememberItemSpawns();
        }

        internal static void DiscardQuestData()
        {
            _triggers?.Clear();
            _triggers = null;
            _questItems?.Clear();
            _questItems = null;
            _itemSpawns.Clear();
        }

        internal static IEnumerable<MapMarkerDef> GetMarkerDefsForPlayer(Player player)
        {
            if (_triggers == null || _questItems == null)
            {
                Plugin.Log.LogWarning($"TriggersWithIds null: {_triggers == null} or QuestItems null: {_questItems == null}");
                return null;
            }

            var markers = new List<MapMarkerDef>();
            foreach (var quest in StartedQuests(player))
            {
                AddQuestMarkers(markers, player, quest);
            }

            return markers;
        }

        /// <summary>
        /// Hideout pins use profile progress and zone positions saved from a previous raid on this map.
        /// </summary>
        internal static IEnumerable<MapMarkerDef> GetMarkerDefsOutOfRaid(Profile profile, MapDef mapDef, MapCanvas mapView)
        {
            if (profile?.QuestsData == null || mapDef == null)
            {
                return Array.Empty<MapMarkerDef>();
            }

            LoadZoneCache();
            _outOfRaidLocations = mapDef.MapInternalNames ?? [];
            _outOfRaidMap = mapView;

            try
            {
                var markers = new List<MapMarkerDef>();
                foreach (var quest in StartedQuests(profile))
                {
                    AddQuestMarkers(markers, null, quest);
                }

                return markers;
            }
            finally
            {
                _outOfRaidLocations = null;
                _outOfRaidMap = null;
            }
        }

        private static void AddQuestMarkers(List<MapMarkerDef> markers, Player player, QuestDataClass quest)
        {
            var completedFinds = CompletedFindTargets(player, quest);
            var questName = quest.Template.NameLocaleKey.BsgLocalized();

            foreach (var condition in OpenConditions(player, quest))
            {
                if (IsLeftoverVisit(condition, completedFinds))
                {
                    continue;
                }

                var description = Describe(condition);
                foreach (var position in PositionsFor(condition))
                {
                    Place(markers, position, questName, description);
                }
            }
        }

        private static IEnumerable<Vector3> PositionsFor(Condition condition)
        {
            switch (condition)
            {
                case ConditionZone zone:
                    foreach (var position in PositionsForZone(zone.zoneId))
                    {
                        yield return position;
                    }
                    break;
                case ConditionLaunchFlare flare:
                    foreach (var position in PositionsForZone(flare.zoneID))
                    {
                        yield return position;
                    }
                    break;
                case ConditionVisitPlace place:
                    foreach (var position in PositionsForZone(place.target))
                    {
                        yield return position;
                    }
                    break;
                case ConditionInZone inside:
                    foreach (var zoneId in inside.zoneIds)
                    {
                        foreach (var position in PositionsForZone(zoneId))
                        {
                            yield return position;
                        }
                    }
                    break;
                case ConditionFindItem find:
                    foreach (var position in PositionsForItems(find.target))
                    {
                        yield return position;
                    }
                    break;
                case ConditionExitName exit:
                    foreach (var position in PositionsForExit(exit.exitName))
                    {
                        yield return position;
                    }
                    break;
                case ConditionCounterCreator counter:
                    var children = counter._templateConditions?.Conditions;
                    if (children == null)
                    {
                        break;
                    }

                    foreach (var child in children)
                    {
                        foreach (var position in PositionsFor(child))
                        {
                            yield return position;
                        }
                    }
                    break;
            }
        }

        private static IEnumerable<Vector3> PositionsForZone(string zoneId)
        {
            if (_triggers != null)
            {
                foreach (var trigger in _triggers)
                {
                    if (trigger != null && trigger.Id == zoneId)
                    {
                        yield return MathUtils.ConvertToMapPosition(trigger.transform.position);
                    }
                }

                yield break;
            }

            LoadZoneCache();
            if (_zoneCache == null || _outOfRaidLocations == null)
            {
                yield break;
            }

            foreach (var location in _outOfRaidLocations)
            {
                if (!_zoneCache.TryGetValue(location, out var zones)
                    || !zones.TryGetValue(zoneId, out var stored))
                {
                    continue;
                }

                foreach (var raw in stored)
                {
                    if (raw == null || raw.Length < 3)
                    {
                        continue;
                    }

                    yield return new Vector3(raw[0], raw[1], raw[2]);
                }
            }
        }

        private static IEnumerable<Vector3> PositionsForExit(string exitName)
        {
            if (string.IsNullOrEmpty(exitName))
            {
                yield break;
            }

            var points = GameUtils.IsInRaid()
                ? Singleton<GameWorld>.Instance?.ExfiltrationController?.ExfiltrationPoints
                : null;
            if (points != null)
            {
                var exit = points.FirstOrDefault(point => point.Settings.Name == exitName);
                if (exit != null)
                {
                    yield return MathUtils.ConvertToMapPosition(exit.transform);
                }

                yield break;
            }

            if (_outOfRaidMap == null)
            {
                yield break;
            }

            foreach (var marker in _outOfRaidMap.GetMapMarkersByCategory(ExtractCategory))
            {
                var text = marker.Text ?? "";
                if (string.Equals(text, exitName, StringComparison.OrdinalIgnoreCase)
                    || text.IndexOf(exitName, StringComparison.OrdinalIgnoreCase) >= 0
                    || exitName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return marker.Position;
                }
            }
        }

        private static IEnumerable<Vector3> PositionsForItems(IEnumerable<string> itemIds)
        {
            if (_questItems == null || itemIds == null)
            {
                yield break;
            }

            foreach (var itemId in itemIds)
            {
                foreach (var loot in _questItems)
                {
                    if (loot != null && loot.TemplateId == itemId)
                    {
                        yield return MathUtils.ConvertToMapPosition(loot.transform.position);
                    }
                }
            }
        }

        private static IEnumerable<Condition> OpenConditions(Player player, QuestDataClass quest)
        {
            if (quest?.Template?.Conditions == null)
            {
                Plugin.Log.LogError("GetIncompleteQuestConditions: quest.Template.Conditions is null, skipping quest");
                yield break;
            }

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

                if (!IsComplete(player, quest, condition))
                {
                    yield return condition;
                }
            }
        }

        private static IEnumerable<QuestDataClass> StartedQuests(Player player)
        {
            var controller = player.QuestController;
            if (controller == null)
            {
                Plugin.Log.LogError($"Not able to get quests for player: {player.Id}, questController is null");
                yield break;
            }

            var quests = controller.Quests;
            if (quests == null)
            {
                Plugin.Log.LogError($"Not able to get quests for player: {player.Id}, quests is null");
                yield break;
            }

            var list = quests.list_1;
            if (list == null)
            {
                Plugin.Log.LogError($"Not able to get quests for player: {player.Id}, questsList is null");
                yield break;
            }

            foreach (var quest in list)
            {
                if (quest?.Template?.Conditions != null && quest.Status == EQuestStatus.Started)
                {
                    yield return quest;
                }
            }
        }

        private static IEnumerable<QuestDataClass> StartedQuests(Profile profile)
        {
            if (profile?.QuestsData == null)
            {
                yield break;
            }

            foreach (var quest in profile.QuestsData)
            {
                if (quest?.Template?.Conditions != null && quest.Status == EQuestStatus.Started)
                {
                    yield return quest;
                }
            }
        }

        /// <summary>
        /// Live progress uses the condition's compare method against the getter's current value.
        /// That matches the quest log. IsConditionDone also demands every necessary child, and those
        /// children often have no progress of their own, so a finished objective still looks open.
        /// CompletedConditions can also keep an id after a quest resets on death, so the live counter wins.
        /// </summary>
        private static bool IsComplete(Player player, QuestDataClass quest, Condition condition)
        {
            if (condition == null || quest == null)
            {
                return false;
            }

            var live = player?.QuestController?.Quests?.GetConditional(quest.Id);
            if (live == null)
            {
                return condition.IsNecessary
                       && quest.CompletedConditions != null
                       && quest.CompletedConditions.Contains(condition.id);
            }

            if (live.IsDone())
            {
                return true;
            }

            if (live.ProgressCheckers != null
                && live.ProgressCheckers.TryGetValue(condition, out var checker)
                && checker != null
                && checker.HasGetter())
            {
                return condition.compareMethod.Compare(checker.CurrentValue, condition.value);
            }

            return live.CompletedConditions != null && live.CompletedConditions.Contains(condition.id);
        }

        private static void RememberItemSpawns()
        {
            if (_questItems == null)
            {
                return;
            }

            foreach (var loot in _questItems)
            {
                if (loot == null)
                {
                    continue;
                }

                var key = loot.TemplateId.ToString();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var position = MathUtils.ConvertToMapPosition(loot.transform.position);
                if (!_itemSpawns.TryGetValue(key, out var spawns))
                {
                    spawns = [];
                    _itemSpawns[key] = spawns;
                }

                if (!spawns.Any(existing => Near(existing, position, SpawnDedupRadius)))
                {
                    spawns.Add(position);
                }
            }
        }

        private static List<string> CompletedFindTargets(Player player, QuestDataClass quest)
        {
            var targets = new List<string>();
            if (quest?.Template?.Conditions == null
                || !quest.Template.Conditions.TryGetValue(EQuestStatus.AvailableForFinish, out var conditions)
                || conditions == null)
            {
                return targets;
            }

            foreach (var condition in conditions)
            {
                if (condition is not ConditionFindItem find || find.target == null || !IsComplete(player, quest, find))
                {
                    continue;
                }

                foreach (var target in find.target)
                {
                    if (!string.IsNullOrEmpty(target) && !targets.Contains(target))
                    {
                        targets.Add(target);
                    }
                }
            }

            return targets;
        }

        /// <summary>
        /// A visit-only counter whose zones sit within <see cref="VisitHintRadius"/> of a FindItem
        /// spawn that is already complete. The item pin leaves when the item is picked up; this hint
        /// would stay on the same spot. If the map opened after the pickup and no spawn was recorded,
        /// the visit is still dropped when none of those items remain in the scene.
        /// </summary>
        private static bool IsLeftoverVisit(Condition condition, List<string> completedFinds)
        {
            if (completedFinds.Count == 0 || !VisitZones(condition, out var zoneIds))
            {
                return false;
            }

            foreach (var zoneId in zoneIds)
            {
                foreach (var zonePos in PositionsForZone(zoneId))
                {
                    foreach (var target in completedFinds)
                    {
                        if (_itemSpawns.TryGetValue(target, out var spawns)
                            && spawns.Any(spawn => Near(spawn, zonePos, VisitHintRadius)))
                        {
                            return true;
                        }
                    }
                }
            }

            if (completedFinds.Any(target => _itemSpawns.TryGetValue(target, out var spawns) && spawns.Count > 0))
            {
                return false;
            }

            return _questItems == null || !_questItems.Any(loot =>
                loot != null && completedFinds.Contains(loot.TemplateId.ToString()));
        }

        private static bool VisitZones(Condition condition, out List<string> zoneIds)
        {
            zoneIds = null;
            if (condition is not ConditionCounterCreator counter)
            {
                return false;
            }

            var children = counter._templateConditions?.Conditions;
            if (children == null)
            {
                return false;
            }

            var zones = new List<string>();
            foreach (var child in children)
            {
                if (child is not ConditionVisitPlace visit || string.IsNullOrEmpty(visit.target))
                {
                    return false;
                }

                zones.Add(visit.target);
            }

            if (zones.Count == 0)
            {
                return false;
            }

            zoneIds = zones;
            return true;
        }

        private static bool Near(Vector3 a, Vector3 b, float radius)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static void SaveZonesForCurrentLocation()
        {
            if (_triggers == null || _triggers.Count == 0)
            {
                return;
            }

            var location = GameUtils.GetCurrentMapInternalName();
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            LoadZoneCache();
            _zoneCache ??= new Dictionary<string, Dictionary<string, List<float[]>>>(StringComparer.OrdinalIgnoreCase);

            var zones = new Dictionary<string, List<float[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (var trigger in _triggers)
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
                Plugin.DebugLog($"Cached {_triggers.Count} quest zone triggers for {location}");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"Failed to persist quest zone cache: {e.Message}");
            }
        }

        private static void LoadZoneCache()
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

        private static void Place(List<MapMarkerDef> markers, Vector3 position, string questName, string description)
        {
            foreach (var marker in markers)
            {
                if (!MathUtils.ApproxEquals(marker.Position.x, position.x)
                    || !MathUtils.ApproxEquals(marker.Position.y, position.y)
                    || !MathUtils.ApproxEquals(marker.Position.z, position.z))
                {
                    continue;
                }

                Append(marker, description);
                return;
            }

            markers.Add(new MapMarkerDef
            {
                Category = Category,
                Color = Settings.QuestColor.Value,
                ImagePath = ImagePath,
                Position = position,
                Pivot = Pivot,
                Text = questName,
                Description = description ?? ""
            });
        }

        private static void Append(MapMarkerDef marker, string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return;
            }

            if (string.IsNullOrEmpty(marker.Description))
            {
                marker.Description = description;
                return;
            }

            if (marker.Description.IndexOf(description, StringComparison.Ordinal) < 0)
            {
                marker.Description = marker.Description + "\n" + description;
            }
        }

        private static string Describe(Condition condition)
        {
            if (condition == null)
            {
                return "";
            }

            string text;
            try
            {
                text = condition.FormattedDescription;
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"Quest condition {condition.id} description failed: {e.Message}");
                return "";
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text = text.Trim();
            return text == condition.id.ToString() ? "" : text;
        }
    }
}
