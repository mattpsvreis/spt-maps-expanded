using System;
using System.Collections.Generic;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
// ServerMapPolicy lives in Core
using MapsExtended.Integration.Mods;
using MapsExtended.Markers;
using MapsExtended.Presentation.Overlays;

namespace MapsExtended.Presentation
{
    /// <summary>
    /// Owns marker provider registration and raid/out-of-raid show/hide lifecycle.
    /// </summary>
    internal sealed class MarkerOrchestrator
    {
        private readonly Dictionary<Type, IMarkerProvider> _providers = new();
        private MapView _mapView;
        private bool _active;
        private bool _inRaid;
        private bool _hostShown;

        internal void Bind(MapView mapView) => _mapView = mapView;

        internal void SetHostShown(bool shown) => _hostShown = shown;

        internal void ApplyConfig(ServerMapPolicy serverConfig)
        {
            AddRemove<PlayerMarkerProvider>(Settings.ShowPlayerMarker.Value);
            AddRemove<QuestMarkerProvider>(Settings.ShowQuestsInRaid.Value);
            AddRemove<LockedDoorMarkerMutator>(Settings.ShowLockedDoorStatus.Value);
            AddRemove<BackpackMarkerProvider>(Settings.ShowDroppedBackpackInRaid.Value);
            AddRemove<BtrMarkerProvider>(Settings.ShowBTRInRaid.Value);
            AddRemove<AirdropMarkerProvider>(Settings.ShowAirdropsInRaid.Value);
            AddRemove<HiddenStashMarkerProvider>(Settings.ShowHiddenStashesInRaid.Value);
            AddRemove<TransitMarkerProvider>(Settings.ShowTransitPointsInRaid.Value);
            AddRemove<SecretMarkerProvider>(Settings.ShowSecretPointsInRaid.Value);

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
                Get<TransitMarkerProvider>()?.RefreshMarkers(_mapView);
            }

            if (Settings.ShowSecretPointsInRaid.Value)
            {
                var provider = Get<SecretMarkerProvider>();
                if (provider != null)
                {
                    provider.ShowExtractStatusInRaid = Settings.ShowExtractStatusInRaid.Value;
                }
            }

            AddRemove<ExtractMarkerProvider>(Settings.ShowExtractsInRaid.Value);
            if (Settings.ShowExtractsInRaid.Value)
            {
                var provider = Get<ExtractMarkerProvider>();
                if (provider != null)
                {
                    provider.ShowExtractStatusInRaid = Settings.ShowExtractStatusInRaid.Value;
                }
            }

            var needOtherPlayerMarkers = Settings.ShowFriendlyPlayerMarkersInRaid.Value
                                      || Settings.ShowEnemyPlayerMarkersInRaid.Value
                                      || Settings.ShowBossMarkersInRaid.Value
                                      || Settings.ShowScavMarkersInRaid.Value
                                      || Settings.ShowNonVanillaBotMarkersInRaid.Value;

            AddRemove<OtherPlayersMarkerProvider>(needOtherPlayerMarkers);
            if (needOtherPlayerMarkers)
            {
                var provider = Get<OtherPlayersMarkerProvider>();
                if (provider != null)
                {
                    provider.ShowFriendlyPlayers = serverConfig != null && serverConfig.AllowShowFriendlyPlayerMarkersInRaid && Settings.ShowFriendlyPlayerMarkersInRaid.Value;
                    provider.ShowEnemyPlayers = serverConfig != null && serverConfig.AllowShowEnemyPlayerMarkersInRaid && Settings.ShowEnemyPlayerMarkersInRaid.Value;
                    provider.ShowScavs = serverConfig != null && serverConfig.AllowShowScavMarkersInRaid && Settings.ShowScavMarkersInRaid.Value;
                    provider.ShowBosses = serverConfig != null && serverConfig.AllowShowBossMarkersInRaid && Settings.ShowBossMarkersInRaid.Value;
                    provider.ShowNonVanillaBots = serverConfig != null && serverConfig.AllowShowNonVanillaBotMarkersInRaid && Settings.ShowNonVanillaBotMarkersInRaid.Value;
                    provider.RefreshMarkers();
                }
            }

            var needCorpseMarkers = Settings.ShowFriendlyCorpsesInRaid.Value
                                 || Settings.ShowKilledCorpsesInRaid.Value
                                 || Settings.ShowFriendlyKilledCorpsesInRaid.Value
                                 || Settings.ShowBossCorpsesInRaid.Value
                                 || Settings.ShowOtherCorpsesInRaid.Value;

            AddRemove<CorpseMarkerProvider>(needCorpseMarkers);
            if (needCorpseMarkers)
            {
                var provider = Get<CorpseMarkerProvider>();
                if (provider != null)
                {
                    provider.ShowFriendlyCorpses = Settings.ShowFriendlyCorpsesInRaid.Value;
                    provider.ShowKilledCorpses = Settings.ShowKilledCorpsesInRaid.Value;
                    provider.ShowFriendlyKilledCorpses = Settings.ShowFriendlyKilledCorpsesInRaid.Value;
                    provider.ShowBossCorpses = Settings.ShowBossCorpsesInRaid.Value;
                    provider.ShowOtherCorpses = Settings.ShowOtherCorpsesInRaid.Value;
                    provider.RefreshMarkers();
                }
            }

            if (ModDetection.HeliCrashLoaded)
            {
                AddRemove<HeliCrashMarkerProvider>(Settings.ShowHeliCrashMarker.Value);
            }

            if (_mapView != null && !Settings.ShowLockedDoorStatus.Value)
            {
                foreach (var marker in _mapView.GetMapMarkersByCategory("LockedDoor"))
                {
                    marker.Color = Settings.LockedDoorDefaultColor.Value;
                }
            }

            foreach (var provider in _providers.Values)
            {
                try
                {
                    provider.OnColorsChanged(_mapView);
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
                foreach (var provider in _providers.Values)
                {
                    try
                    {
                        if (_inRaid)
                        {
                            provider.OnHideInRaid(_mapView);
                        }
                        else
                        {
                            provider.OnHideOutOfRaid(_mapView);
                        }
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
                foreach (var provider in _providers.Values)
                {
                    try
                    {
                        if (inRaid)
                        {
                            provider.OnShowInRaid(_mapView);
                        }
                        else
                        {
                            provider.OnShowOutOfRaid(_mapView);
                        }
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
            foreach (var provider in _providers.Values)
            {
                try
                {
                    provider.OnMapChanged(_mapView, mapDef);
                }
                catch (Exception e)
                {
                    Plugin.Log?.LogError($"Failed to change map markers: {e}");
                }
            }
        }

        internal void NotifyRaidEnd()
        {
            foreach (var provider in _providers.Values)
            {
                try
                {
                    provider.OnRaidEnd(_mapView);
                }
                catch (Exception e)
                {
                    Plugin.Log?.LogError($"Failed to clear raid markers: {e}");
                }
            }
        }

        internal void AddRemove<T>(bool status) where T : IMarkerProvider, new()
        {
            if (status && !_providers.ContainsKey(typeof(T)))
            {
                _providers[typeof(T)] = new T();
                if (_hostShown && GameUtils.IsInRaid())
                {
                    _providers[typeof(T)].OnShowInRaid(_mapView);
                }
                else if (_hostShown && !GameUtils.IsInRaid())
                {
                    _providers[typeof(T)].OnShowOutOfRaid(_mapView);
                }
            }
            else if (!status && _providers.ContainsKey(typeof(T)))
            {
                _providers[typeof(T)].OnDisable(_mapView);
                _providers.Remove(typeof(T));
            }
        }

        private T Get<T>() where T : class, IMarkerProvider
        {
            return _providers.TryGetValue(typeof(T), out var provider) ? provider as T : null;
        }
    }
}
