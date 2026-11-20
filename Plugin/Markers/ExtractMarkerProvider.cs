using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class ExtractMarkerProvider : MarkerProviderBase
    {
        // TODO: move to config
        private const string ExtractCategory = "Extract";
        private const string ExtractImagePath = "Markers/exit.png";
        //

        private bool _showExtractStatusInRaid = true;
        public bool ShowExtractStatusInRaid
        {
            get
            {
                return _showExtractStatusInRaid;
            }

            set
            {
                if (_showExtractStatusInRaid == value)
                {
                    return;
                }

                _showExtractStatusInRaid = value;

                // force update all statuses
                foreach (var extract in _extractMarkers.Keys)
                {
                    UpdateExtractStatus(extract, extract.Status);
                }
            }
        }

        // Factory (and some other maps) drop two triggers a few meters apart for one extract.
        // Each used to get its own icon and label, so the names drew on top of each other.
        // Same name within this distance is one marker. A second entrance farther away stays.
        private const float SameExtractRadius = 15f;

        private readonly Dictionary<ExfiltrationPoint, MapMarker> _extractMarkers = [];
        private readonly Dictionary<MapMarker, List<ExfiltrationPoint>> _pointsByMarker = [];

        public override void OnShowInRaid(MapView map)
        {
            // get valid extracts only on first time that this is run in a raid
            if (_extractMarkers.Count == 0)
            {
                AddExtractMarkers(map);
            }

            foreach (var extract in _extractMarkers.Keys)
            {
                // update color based on exfil status
                UpdateExtractStatus(extract, extract.Status);

                // subscribe to status changes while map is shown
                extract.OnStatusChanged += UpdateExtractStatus;
            }
        }

        public override void OnHideInRaid(MapView map)
        {
            // unsubscribe from updates while map is hidden
            foreach (var extract in _extractMarkers.Keys)
            {
                extract.OnStatusChanged -= UpdateExtractStatus;
            }
        }

        public override void OnRaidEnd(MapView map)
        {
            TryRemoveMarkers();
        }

        public override void OnMapChanged(MapView map, MapDef mapDef)
        {
            var points = _extractMarkers.Keys.ToList();
            ClearMarkers();
            PlaceExtractMarkers(map, points);
        }

        public override void OnDisable(MapView map)
        {
            TryRemoveMarkers();
        }

        private void AddExtractMarkers(MapView map)
        {
            var gameWorld = Singleton<GameWorld>.Instance;
            var player = GameUtils.GetMainPlayer();

            IEnumerable<ExfiltrationPoint> extracts;
            if (GameUtils.IsScavRaid())
            {
                extracts = gameWorld.ExfiltrationController.ScavExfiltrationPoints
                                .Where(p => p != null && p.isActiveAndEnabled && p.InfiltrationMatch(player));
            }
            else
            {
                extracts = gameWorld.ExfiltrationController.ExfiltrationPoints
                                .Where(p => p != null && p.isActiveAndEnabled && p.InfiltrationMatch(player));
            }

            PlaceExtractMarkers(map, extracts);
        }

        private void PlaceExtractMarkers(MapView map, IEnumerable<ExfiltrationPoint> extracts)
        {
            foreach (var cluster in ClusterExtracts(extracts))
            {
                var markerDef = new MapMarkerDef
                {
                    Category = ExtractCategory,
                    ImagePath = ExtractImagePath,
                    Text = cluster[0].Settings.Name.BsgLocalized(),
                    Position = ClusterPosition(cluster)
                };

                var marker = map.AddMapMarker(markerDef);
                _pointsByMarker[marker] = cluster;
                foreach (var extract in cluster)
                {
                    _extractMarkers[extract] = marker;
                }

                UpdateExtractStatus(cluster[0], cluster[0].Status);
            }
        }

        private static List<List<ExfiltrationPoint>> ClusterExtracts(IEnumerable<ExfiltrationPoint> extracts)
        {
            var pending = extracts.ToList();
            var clusters = new List<List<ExfiltrationPoint>>();
            var radiusSq = SameExtractRadius * SameExtractRadius;

            while (pending.Count > 0)
            {
                var cluster = new List<ExfiltrationPoint> { pending[pending.Count - 1] };
                pending.RemoveAt(pending.Count - 1);
                var key = ExtractKey(cluster[0]);

                var grew = true;
                while (grew)
                {
                    grew = false;
                    for (var i = pending.Count - 1; i >= 0; i--)
                    {
                        var candidate = pending[i];
                        if (ExtractKey(candidate) != key || !IsNearCluster(candidate, cluster, radiusSq))
                        {
                            continue;
                        }

                        cluster.Add(candidate);
                        pending.RemoveAt(i);
                        grew = true;
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        private static bool IsNearCluster(ExfiltrationPoint candidate, List<ExfiltrationPoint> cluster, float radiusSq)
        {
            var candidatePos = MathUtils.ConvertToMapPosition(candidate.transform);
            foreach (var member in cluster)
            {
                var memberPos = MathUtils.ConvertToMapPosition(member.transform);
                var dx = candidatePos.x - memberPos.x;
                var dy = candidatePos.y - memberPos.y;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 ClusterPosition(List<ExfiltrationPoint> cluster)
        {
            var sum = Vector3.zero;
            var positions = new Vector3[cluster.Count];
            for (var i = 0; i < cluster.Count; i++)
            {
                positions[i] = MathUtils.ConvertToMapPosition(cluster[i].transform);
                sum += positions[i];
            }

            var mean = sum / cluster.Count;
            var height = positions[0].z;
            var best = Mathf.Abs(height - mean.z);
            for (var i = 1; i < positions.Length; i++)
            {
                var delta = Mathf.Abs(positions[i].z - mean.z);
                if (delta < best)
                {
                    best = delta;
                    height = positions[i].z;
                }
            }

            return new Vector3(mean.x, mean.y, height);
        }

        private static string ExtractKey(ExfiltrationPoint point)
        {
            var name = point.Settings?.Name;
            return string.IsNullOrEmpty(name) ? point.GetInstanceID().ToString() : name;
        }

        private void TryRemoveMarkers()
        {
            foreach (var extract in _extractMarkers.Keys)
            {
                extract.OnStatusChanged -= UpdateExtractStatus;
            }

            ClearMarkers();
        }

        private void ClearMarkers()
        {
            foreach (var marker in _pointsByMarker.Keys)
            {
                marker.ContainingMapView?.RemoveMapMarker(marker);
            }

            _extractMarkers.Clear();
            _pointsByMarker.Clear();
        }

        private void UpdateExtractStatus(ExfiltrationPoint extract, EExfiltrationStatus status)
        {
            if (!_extractMarkers.TryGetValue(extract, out var marker) || !_pointsByMarker.TryGetValue(marker, out var points))
            {
                return;
            }

            if (!_showExtractStatusInRaid)
            {
                marker.Color = Settings.ExtractDefaultColor.Value;
                return;
            }

            var best = EExfiltrationStatus.NotPresent;
            var bestRank = -1;
            foreach (var point in points)
            {
                var rank = StatusRank(point.Status);
                if (rank <= bestRank)
                {
                    continue;
                }

                bestRank = rank;
                best = point.Status;
            }

            switch (best)
            {
                case EExfiltrationStatus.NotPresent:
                    marker.Color = Settings.ExtractClosedColor.Value;
                    break;
                case EExfiltrationStatus.UncompleteRequirements:
                    marker.Color = Settings.ExtractHasRequirementsColor.Value;
                    break;
                default:
                    marker.Color = Settings.ExtractOpenColor.Value;
                    break;
            }
        }

        private static int StatusRank(EExfiltrationStatus status)
        {
            switch (status)
            {
                case EExfiltrationStatus.NotPresent:
                    return 0;
                case EExfiltrationStatus.UncompleteRequirements:
                    return 1;
                default:
                    return 2;
            }
        }
    }
}
