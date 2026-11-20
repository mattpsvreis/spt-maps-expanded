using System;
using System.Reflection;
using HarmonyLib;
using MapsExtended.Presentation;
using MapsExtended.Integration;

namespace MapsExtended.Integration
{
    /// <summary>
    /// When MapVariants decides or changes the raid variant, re-run the location
    /// filter so MapAlias can pick the matching Extended pack (late decisions / transits).
    /// </summary>
    internal static class VariantRefresh
    {
        private static Action<string> _handler;
        private static EventInfo _resolvedEvent;
        private static bool _subscribed;
        private static PropertyInfo _currentLocationId;

        internal static void Initialize()
        {
            if (!PluginDetection.Loaded(MapAlias.MapVariantsGuid))
            {
                return;
            }

            try
            {
                var mapsType = AccessTools.TypeByName("MapVariants.Client.Api.Maps")
                               ?? throw new MissingMemberException("MapVariants.Client.Api.Maps not found");

                _resolvedEvent = mapsType.GetEvent("VariantResolved", BindingFlags.Public | BindingFlags.Static)
                                 ?? throw new MissingMemberException("Maps.VariantResolved not found");

                _currentLocationId = AccessTools.Property(mapsType, "CurrentLocationId");
                _handler = OnVariantResolved;
                _resolvedEvent.AddEventHandler(null, _handler);
                _subscribed = true;
                Plugin.Log?.LogInfo("[MapsExtended] Subscribed to MapVariants VariantResolved for live map-pack refresh.");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"[MapsExtended] Could not subscribe to MapVariants VariantResolved: {e.Message}");
            }
        }

        internal static void Shutdown()
        {
            if (!_subscribed || _resolvedEvent == null || _handler == null)
            {
                return;
            }

            try
            {
                _resolvedEvent.RemoveEventHandler(null, _handler);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"[MapsExtended] VariantResolved unsubscribe failed: {e.Message}");
            }

            _subscribed = false;
            _handler = null;
        }

        private static void OnVariantResolved(string variant)
        {
            try
            {
                var locationId = ResolveLocationId();
                if (string.IsNullOrWhiteSpace(locationId))
                {
                    return;
                }

                if (!RefreshScreen(Plugin.Instance?.Map, locationId))
                {
                    var screens = UnityEngine.Resources.FindObjectsOfTypeAll<RaidMapHost>();
                    if (screens == null)
                    {
                        return;
                    }

                    foreach (var screen in screens)
                    {
                        RefreshScreen(screen, locationId);
                    }
                }

                Plugin.DebugLog($"VariantResolved variant={variant}, location={locationId}");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"[MapsExtended] VariantResolved refresh failed: {e.Message}");
            }
        }

        private static string ResolveLocationId()
        {
            if (_currentLocationId?.GetValue(null) is string fromMv && !string.IsNullOrWhiteSpace(fromMv))
            {
                return fromMv;
            }

            return GameUtils.GetCurrentMapInternalName();
        }

        private static bool RefreshScreen(RaidMapHost screen, string locationId)
        {
            if (screen == null)
            {
                return false;
            }

            screen.RefreshLocationFilter(locationId);
            return true;
        }
    }
}
