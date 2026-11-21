using System;
using System.Reflection;
using HarmonyLib;
using MapsExtended.Presentation;
using UnityEngine;

namespace MapsExtended.Integration
{
    /// <summary>
    /// MapVariants can lock a raid variant after the map screen is already open.
    /// This listens for that decision and asks the open screen to filter packs again.
    /// </summary>
    internal static class VariantRefresh
    {
        private static Action<string> _onResolved;
        private static EventInfo _variantResolved;
        private static PropertyInfo _currentLocationId;
        private static bool _hooked;

        internal static void Initialize()
        {
            if (!PluginDetection.Loaded(MapAlias.MapVariantsGuid))
            {
                return;
            }

            try
            {
                var maps = AccessTools.TypeByName("MapVariants.Client.Api.Maps");
                if (maps == null)
                {
                    throw new MissingMemberException("MapVariants.Client.Api.Maps not found");
                }

                _variantResolved = maps.GetEvent("VariantResolved", BindingFlags.Public | BindingFlags.Static);
                if (_variantResolved == null)
                {
                    throw new MissingMemberException("Maps.VariantResolved not found");
                }

                _currentLocationId = AccessTools.Property(maps, "CurrentLocationId");
                _onResolved = HandleResolved;
                _variantResolved.AddEventHandler(null, _onResolved);
                _hooked = true;
                Plugin.Log?.LogInfo("[MapsExpanded] Subscribed to MapVariants VariantResolved for live map-pack refresh.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[MapsExpanded] Could not subscribe to MapVariants VariantResolved: " + ex.Message);
            }
        }

        internal static void Shutdown()
        {
            if (!_hooked || _variantResolved == null || _onResolved == null)
            {
                return;
            }

            try
            {
                _variantResolved.RemoveEventHandler(null, _onResolved);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[MapsExpanded] VariantResolved unsubscribe failed: " + ex.Message);
            }

            _hooked = false;
            _onResolved = null;
        }

        private static void HandleResolved(string variant)
        {
            try
            {
                var locationId = ReadLocation();
                if (string.IsNullOrWhiteSpace(locationId))
                {
                    return;
                }

                if (!Apply(Plugin.Instance?.Map, locationId))
                {
                    var open = Resources.FindObjectsOfTypeAll<RaidMapHost>();
                    if (open == null)
                    {
                        return;
                    }

                    for (var i = 0; i < open.Length; i++)
                    {
                        Apply(open[i], locationId);
                    }
                }

                Plugin.DebugLog("VariantResolved variant=" + variant + ", location=" + locationId);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[MapsExpanded] VariantResolved refresh failed: " + ex.Message);
            }
        }

        private static string ReadLocation()
        {
            if (_currentLocationId?.GetValue(null) is string fromApi && !string.IsNullOrWhiteSpace(fromApi))
            {
                return fromApi;
            }

            return GameUtils.GetCurrentMapInternalName();
        }

        private static bool Apply(RaidMapHost screen, string locationId)
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
