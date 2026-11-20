using System;
using System.Reflection;
using HarmonyLib;
using MapsExtended.UI;
using MapsExtended.UI.Controls;
using MapsExtended.Utils;

namespace MapsExtended.Features
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
        private static FieldInfo _dropdownField;

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
                _dropdownField = AccessTools.Field(typeof(ModdedMapScreen), "_mapSelectDropdown");
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
                    // Fallback if Instance.Map isn't attached yet (rare race on first resolve).
                    var screens = UnityEngine.Resources.FindObjectsOfTypeAll<ModdedMapScreen>();
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

        private static bool RefreshScreen(ModdedMapScreen screen, string locationId)
        {
            if (screen == null || _dropdownField == null)
            {
                return false;
            }

            if (_dropdownField.GetValue(screen) is not MapSelectDropdown dropdown)
            {
                return false;
            }

            dropdown.FilterByInternalMapName(locationId);
            dropdown.LoadPreferredMap(locationId);
            return true;
        }
    }
}
