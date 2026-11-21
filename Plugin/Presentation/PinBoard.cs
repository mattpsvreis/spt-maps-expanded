using System;
using System.Collections.Generic;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Integration.Mods;
using MapsExtended.Markers;
using MapsExtended.Presentation.Overlays;

namespace MapsExtended.Presentation
{
    /// <summary>
    /// Turns F12 and server policy into active pin sources for the open canvas.
    /// </summary>
    internal sealed class PinBoard
    {
        private readonly Dictionary<Type, PinSource> _sources = new();
        private MapCanvas _map;
        private bool _active;
        private bool _inRaid;
        private bool _hostShown;

        internal void Bind(MapCanvas map) => _map = map;

        internal void SetHostShown(bool shown) => _hostShown = shown;

        internal void ApplyConfig(ServerMapPolicy serverConfig)
        {
            Toggle<PlayerMarkerProvider>(Settings.ShowPlayerMarker.Value);
            Toggle<QuestMarkerProvider>(Settings.ShowQuestsInRaid.Value);
            Toggle<LockedDoorMarkerMutator>(Settings.ShowLockedDoorStatus.Value);
            Toggle<BackpackMarkerProvider>(Settings.ShowDroppedBackpackInRaid.Value);
            Toggle<BtrMarkerProvider>(Settings.ShowBTRInRaid.Value);
            Toggle<AirdropMarkerProvider>(Settings.ShowAirdropsInRaid.Value);
            Toggle<HiddenStashMarkerProvider>(Settings.ShowHiddenStashesInRaid.Value);
            Toggle<TransitMarkerProvider>(Settings.ShowTransitPointsInRaid.Value);
            Toggle<SecretMarkerProvider>(Settings.ShowSecretPointsInRaid.Value);

            if (Settings.ShowAirdropsInRaid.Value)
            {
                Get<AirdropMarkerProvider>()?.RefreshMarkers();
            }

            if (Settings.ShowHiddenStashesInRaid.Value)
            {
                Get<HiddenStashMarkerProvider>()?.RefreshMarkers();
            }

            if (Settings.ShowTransitPointsInRaid.Value)
            {
                Get<TransitMarkerProvider>()?.RefreshMarkers(_map);
            }

            if (Settings.ShowSecretPointsInRaid.Value)
            {
                var secrets = Get<SecretMarkerProvider>();
                if (secrets != null)
                {
                    secrets.ShowExtractStatusInRaid = Settings.ShowExtractStatusInRaid.Value;
                }
            }

            Toggle<ExtractMarkerProvider>(Settings.ShowExtractsInRaid.Value);
            if (Settings.ShowExtractsInRaid.Value)
            {
                var extracts = Get<ExtractMarkerProvider>();
                if (extracts != null)
                {
                    extracts.ShowExtractStatusInRaid = Settings.ShowExtractStatusInRaid.Value;
                }
            }

            var needOthers = Settings.ShowFriendlyPlayerMarkersInRaid.Value
                             || Settings.ShowEnemyPlayerMarkersInRaid.Value
                             || Settings.ShowBossMarkersInRaid.Value
                             || Settings.ShowScavMarkersInRaid.Value
                             || Settings.ShowNonVanillaBotMarkersInRaid.Value;

            Toggle<OtherPlayersMarkerProvider>(needOthers);
            if (needOthers)
            {
                var others = Get<OtherPlayersMarkerProvider>();
                if (others != null)
                {
                    others.ShowFriendlyPlayers = serverConfig != null && serverConfig.AllowShowFriendlyPlayerMarkersInRaid && Settings.ShowFriendlyPlayerMarkersInRaid.Value;
                    others.ShowEnemyPlayers = serverConfig != null && serverConfig.AllowShowEnemyPlayerMarkersInRaid && Settings.ShowEnemyPlayerMarkersInRaid.Value;
                    others.ShowScavs = serverConfig != null && serverConfig.AllowShowScavMarkersInRaid && Settings.ShowScavMarkersInRaid.Value;
                    others.ShowBosses = serverConfig != null && serverConfig.AllowShowBossMarkersInRaid && Settings.ShowBossMarkersInRaid.Value;
                    others.ShowNonVanillaBots = serverConfig != null && serverConfig.AllowShowNonVanillaBotMarkersInRaid && Settings.ShowNonVanillaBotMarkersInRaid.Value;
                    others.RefreshMarkers();
                }
            }

            var needCorpses = Settings.ShowFriendlyCorpsesInRaid.Value
                              || Settings.ShowKilledCorpsesInRaid.Value
                              || Settings.ShowFriendlyKilledCorpsesInRaid.Value
                              || Settings.ShowBossCorpsesInRaid.Value
                              || Settings.ShowOtherCorpsesInRaid.Value;

            Toggle<CorpseMarkerProvider>(needCorpses);
            if (needCorpses)
            {
                var corpses = Get<CorpseMarkerProvider>();
                if (corpses != null)
                {
                    corpses.ShowFriendlyCorpses = Settings.ShowFriendlyCorpsesInRaid.Value;
                    corpses.ShowKilledCorpses = Settings.ShowKilledCorpsesInRaid.Value;
                    corpses.ShowFriendlyKilledCorpses = Settings.ShowFriendlyKilledCorpsesInRaid.Value;
                    corpses.ShowBossCorpses = Settings.ShowBossCorpsesInRaid.Value;
                    corpses.ShowOtherCorpses = Settings.ShowOtherCorpsesInRaid.Value;
                    corpses.RefreshMarkers();
                }
            }

            if (ModDetection.HeliCrashLoaded)
            {
                Toggle<HeliCrashMarkerProvider>(Settings.ShowHeliCrashMarker.Value);
            }

            if (_map != null && !Settings.ShowLockedDoorStatus.Value)
            {
                foreach (var marker in _map.GetMapMarkersByCategory("LockedDoor"))
                {
                    marker.Color = Settings.LockedDoorDefaultColor.Value;
                }
            }

            foreach (var source in _sources.Values)
            {
                try
                {
                    source.Recolor(_map);
                }
                catch (Exception e)
                {
                    Plugin.Log?.LogError($"Failed to recolor map markers: {e}");
                }
            }
        }

        internal void SetVisibility(bool visible, bool inRaid)
        {
            if (_active && (!visible || inRaid != _inRaid))
            {
                foreach (var source in _sources.Values)
                {
                    try
                    {
                        source.Hidden(_map, _inRaid);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log?.LogError($"Failed to hide map markers: {e}");
                    }
                }

                _active = false;
            }

            if (visible && !_active)
            {
                _inRaid = inRaid;
                _active = true;
                foreach (var source in _sources.Values)
                {
                    try
                    {
                        source.Shown(_map, inRaid);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log?.LogError($"Failed to show map markers: {e}");
                    }
                }
            }
        }

        internal void NotifyMapChanged(MapDef mapDef)
        {
            foreach (var source in _sources.Values)
            {
                try
                {
                    source.MapSwapped(_map, mapDef);
                }
                catch (Exception e)
                {
                    Plugin.Log?.LogError($"Failed to change map markers: {e}");
                }
            }
        }

        internal void NotifyRaidEnd()
        {
            foreach (var source in _sources.Values)
            {
                try
                {
                    source.RaidEnded(_map);
                }
                catch (Exception e)
                {
                    Plugin.Log?.LogError($"Failed to clear raid markers: {e}");
                }
            }
        }

        private void Toggle<T>(bool enabled) where T : PinSource, new()
        {
            if (enabled && !_sources.ContainsKey(typeof(T)))
            {
                _sources[typeof(T)] = new T();
                if (_hostShown)
                {
                    _sources[typeof(T)].Shown(_map, GameUtils.IsInRaid());
                }
            }
            else if (!enabled && _sources.ContainsKey(typeof(T)))
            {
                _sources[typeof(T)].TurnedOff(_map);
                _sources.Remove(typeof(T));
            }
        }

        private T Get<T>() where T : PinSource
        {
            return _sources.TryGetValue(typeof(T), out var source) ? source as T : null;
        }
    }
}
