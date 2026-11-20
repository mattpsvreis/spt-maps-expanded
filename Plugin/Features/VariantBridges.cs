using System;
using System.Reflection;
using HarmonyLib;

namespace MapsExtended.Features
{
    internal readonly struct VariantDecision
    {
        internal readonly bool ApiAvailable;
        internal readonly bool Managed;
        internal readonly bool Decided;
        internal readonly bool IsBackport;

        internal VariantDecision(bool apiAvailable, bool managed, bool decided, bool isBackport)
        {
            ApiAvailable = apiAvailable;
            Managed = managed;
            Decided = decided;
            IsBackport = isBackport;
        }

        public override string ToString()
            => $"api={ApiAvailable}, managed={Managed}, decided={Decided}, backport={IsBackport}";
    }

    internal static class MapVariantsBridge
    {
        private static Type _type;
        private static bool _searched;
        private static MethodInfo _isManaged;
        private static PropertyInfo _currentLocationId;
        private static PropertyInfo _decided;
        private static PropertyInfo _isBackport;

        private static Type ApiType
        {
            get
            {
                if (!_searched)
                {
                    _searched = true;
                    _type = AccessTools.TypeByName("MapVariants.Client.Api.Maps");
                    if (_type != null)
                    {
                        _isManaged = AccessTools.Method(_type, "IsManaged", new[] { typeof(string) });
                        _currentLocationId = AccessTools.Property(_type, "CurrentLocationId");
                        _decided = AccessTools.Property(_type, "Decided");
                        _isBackport = AccessTools.Property(_type, "IsBackport");
                    }
                }

                return _type;
            }
        }

        internal static VariantDecision Resolve(string locationId)
        {
            if (ApiType == null)
            {
                return new VariantDecision(false, false, false, false);
            }

            try
            {
                var managed = _isManaged != null && (bool)_isManaged.Invoke(null, new object[] { locationId });
                if (!managed)
                {
                    return new VariantDecision(true, false, false, false);
                }

                var currentLocation = _currentLocationId?.GetValue(null) as string;
                if (string.IsNullOrEmpty(currentLocation) ||
                    !string.Equals(Normalize(currentLocation), Normalize(locationId), StringComparison.OrdinalIgnoreCase))
                {
                    return new VariantDecision(true, true, false, false);
                }

                if (_decided?.GetValue(null) is not bool decided || !decided)
                {
                    return new VariantDecision(true, true, false, false);
                }

                var backport = _isBackport?.GetValue(null) is bool isBackport && isBackport;
                return new VariantDecision(true, true, true, backport);
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"MapVariants API read failed; leaving location unchanged: {e.Message}");
                return new VariantDecision(true, true, false, false);
            }
        }

        private static string Normalize(string value)
            => value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    internal static class FactoryClassicBridge
    {
        private static Type _type;
        private static bool _searched;
        private static PropertyInfo _classicSceneLoaded;
        private static PropertyInfo _decided;
        private static PropertyInfo _isClassic;

        private static Type ApiType
        {
            get
            {
                if (!_searched)
                {
                    _searched = true;
                    _type = AccessTools.TypeByName("FactoryClassic.Client.Api.Factory");
                    if (_type != null)
                    {
                        _classicSceneLoaded = AccessTools.Property(_type, "ClassicSceneLoaded");
                        _decided = AccessTools.Property(_type, "Decided");
                        _isClassic = AccessTools.Property(_type, "IsClassic");
                    }
                }

                return _type;
            }
        }

        internal static bool IsFactory(string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                return false;
            }

            return locationId.Equals("factory4_day", StringComparison.OrdinalIgnoreCase)
                   || locationId.Equals("factory4_night", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ClassicSceneLoaded
        {
            get
            {
                if (ApiType == null)
                {
                    return false;
                }

                try
                {
                    return _classicSceneLoaded?.GetValue(null) is bool b && b;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static bool IsClassicChosen
        {
            get
            {
                if (ApiType == null)
                {
                    return false;
                }

                try
                {
                    if (_decided?.GetValue(null) is not bool decided || !decided)
                    {
                        return false;
                    }

                    return _isClassic?.GetValue(null) is bool classic && classic;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
