using System;
using System.Reflection;
using HarmonyLib;

namespace MapsExtended.Integration
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
        {
            return "api=" + ApiAvailable
                   + ", managed=" + Managed
                   + ", decided=" + Decided
                   + ", backport=" + IsBackport;
        }
    }

    internal static class MapVariantsBridge
    {
        private const string ApiTypeName = "MapVariants.Client.Api.Maps";

        private static bool _probed;
        private static Type _mapsType;
        private static MethodInfo _isManaged;
        private static PropertyInfo _currentLocationId;
        private static PropertyInfo _decided;
        private static PropertyInfo _isBackport;

        internal static VariantDecision Resolve(string locationId)
        {
            if (!Probe())
            {
                return Absent();
            }

            try
            {
                if (!CallManaged(locationId))
                {
                    return new VariantDecision(true, false, false, false);
                }

                if (!LocationMatches(locationId) || !ReadFlag(_decided))
                {
                    return new VariantDecision(true, true, false, false);
                }

                return new VariantDecision(true, true, true, ReadFlag(_isBackport));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("MapVariants API read failed; leaving location unchanged: " + ex.Message);
                return new VariantDecision(true, true, false, false);
            }
        }

        private static bool Probe()
        {
            if (_probed)
            {
                return _mapsType != null;
            }

            _probed = true;
            _mapsType = AccessTools.TypeByName(ApiTypeName);
            if (_mapsType == null)
            {
                return false;
            }

            _isManaged = AccessTools.Method(_mapsType, "IsManaged", new[] { typeof(string) });
            _currentLocationId = AccessTools.Property(_mapsType, "CurrentLocationId");
            _decided = AccessTools.Property(_mapsType, "Decided");
            _isBackport = AccessTools.Property(_mapsType, "IsBackport");
            return true;
        }

        private static bool CallManaged(string locationId)
        {
            if (_isManaged == null)
            {
                return false;
            }

            return (bool)_isManaged.Invoke(null, new object[] { locationId });
        }

        private static bool LocationMatches(string locationId)
        {
            var current = _currentLocationId?.GetValue(null) as string;
            if (string.IsNullOrEmpty(current))
            {
                return false;
            }

            return string.Equals(Fold(current), Fold(locationId), StringComparison.OrdinalIgnoreCase);
        }

        private static bool ReadFlag(PropertyInfo property)
        {
            return property?.GetValue(null) is bool value && value;
        }

        private static string Fold(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.Trim().ToLowerInvariant();
        }

        private static VariantDecision Absent()
        {
            return new VariantDecision(false, false, false, false);
        }
    }

    internal static class FactoryClassicBridge
    {
        private const string ApiTypeName = "FactoryClassic.Client.Api.Factory";

        private static bool _probed;
        private static Type _factoryType;
        private static PropertyInfo _classicSceneLoaded;
        private static PropertyInfo _decided;
        private static PropertyInfo _isClassic;

        internal static bool IsFactory(string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                return false;
            }

            if (locationId.Equals("factory4_day", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return locationId.Equals("factory4_night", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ClassicSceneLoaded
        {
            get { return ReadLoadedFlag(); }
        }

        internal static bool IsClassicChosen
        {
            get { return ReadChosenFlag(); }
        }

        private static bool ReadLoadedFlag()
        {
            if (!Probe())
            {
                return false;
            }

            try
            {
                return _classicSceneLoaded?.GetValue(null) is bool loaded && loaded;
            }
            catch
            {
                return false;
            }
        }

        private static bool ReadChosenFlag()
        {
            if (!Probe())
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

        private static bool Probe()
        {
            if (_probed)
            {
                return _factoryType != null;
            }

            _probed = true;
            _factoryType = AccessTools.TypeByName(ApiTypeName);
            if (_factoryType == null)
            {
                return false;
            }

            _classicSceneLoaded = AccessTools.Property(_factoryType, "ClassicSceneLoaded");
            _decided = AccessTools.Property(_factoryType, "Decided");
            _isClassic = AccessTools.Property(_factoryType, "IsClassic");
            return true;
        }
    }
}
