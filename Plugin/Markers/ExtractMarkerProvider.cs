using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class ExtractMarkerProvider : PinSource
    {
        private const string Icon = "Markers/exit.png";
        private const float SameExtractRadius = 15f;

        private readonly Dictionary<ExfiltrationPoint, MapMarker> _byPoint = [];
        private readonly Dictionary<MapMarker, List<ExfiltrationPoint>> _byMarker = [];
        private readonly Dictionary<ExfiltrationPoint, WorldInteractiveObjectInteract> _switches = [];
        private bool _statusTint = true;

        public bool ShowExtractStatusInRaid
        {
            get => _statusTint;
            set
            {
                if (_statusTint == value)
                {
                    return;
                }

                _statusTint = value;
                foreach (var extract in _byPoint.Keys)
                {
                    Paint(extract, extract.Status);
                }
            }
        }

        protected override void ShowInRaid(MapCanvas map)
        {
            if (_byPoint.Count == 0)
            {
                PlaceAll(map);
            }

            foreach (var extract in _byPoint.Keys)
            {
                Paint(extract, extract.Status);
                extract.OnStatusChanged += Paint;
                HookSwitches(extract);
            }
        }

        protected override void HideInRaid(MapCanvas map)
        {
            foreach (var extract in _byPoint.Keys)
            {
                extract.OnStatusChanged -= Paint;
                UnhookSwitches(extract);
            }
        }

        protected override void EndRaid(MapCanvas map) => Clear();

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            var points = _byPoint.Keys.ToList();
            DropMarkers();
            Place(map, points);
        }

        protected override void Disable(MapCanvas map) => Clear();

        protected override void ColorsChanged(MapCanvas map)
        {
            foreach (var extract in _byPoint.Keys)
            {
                Paint(extract, extract.Status);
            }
        }

        private void PlaceAll(MapCanvas map)
        {
            var world = Singleton<GameWorld>.Instance;
            var player = GameUtils.GetMainPlayer();
            var extracts = GameUtils.IsScavRaid()
                ? world.ExfiltrationController.ScavExfiltrationPoints
                    .Where(point => point != null && point.isActiveAndEnabled && point.InfiltrationMatch(player))
                : world.ExfiltrationController.ExfiltrationPoints
                    .Where(point => point != null && point.isActiveAndEnabled && point.InfiltrationMatch(player));
            Place(map, extracts);
        }

        private void Place(MapCanvas map, IEnumerable<ExfiltrationPoint> extracts)
        {
            foreach (var cluster in Cluster(extracts))
            {
                var marker = map.AddMapMarker(new MapMarkerDef
                {
                    Category = "Extract",
                    ImagePath = Icon,
                    Text = cluster[0].Settings.Name.BsgLocalized(),
                    Position = ClusterCenter(cluster)
                });
                _byMarker[marker] = cluster;
                foreach (var extract in cluster)
                {
                    _byPoint[extract] = marker;
                }

                Paint(cluster[0], cluster[0].Status);
            }
        }

        private static List<List<ExfiltrationPoint>> Cluster(IEnumerable<ExfiltrationPoint> extracts)
        {
            var pending = extracts.ToList();
            var clusters = new List<List<ExfiltrationPoint>>();
            var radiusSq = SameExtractRadius * SameExtractRadius;
            while (pending.Count > 0)
            {
                var cluster = new List<ExfiltrationPoint> { pending[^1] };
                pending.RemoveAt(pending.Count - 1);
                var key = Key(cluster[0]);
                var grew = true;
                while (grew)
                {
                    grew = false;
                    for (var i = pending.Count - 1; i >= 0; i--)
                    {
                        var candidate = pending[i];
                        if (Key(candidate) != key || !Near(candidate, cluster, radiusSq))
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

        private static bool Near(ExfiltrationPoint candidate, List<ExfiltrationPoint> cluster, float radiusSq)
        {
            var pos = MathUtils.ConvertToMapPosition(candidate.transform);
            foreach (var member in cluster)
            {
                var other = MathUtils.ConvertToMapPosition(member.transform);
                var dx = pos.x - other.x;
                var dy = pos.y - other.y;
                if (dx * dx + dy * dy <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 ClusterCenter(List<ExfiltrationPoint> cluster)
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

        private static string Key(ExfiltrationPoint point)
        {
            var name = point.Settings?.Name;
            return string.IsNullOrEmpty(name) ? point.GetInstanceID().ToString() : name;
        }

        private void Clear()
        {
            foreach (var extract in _byPoint.Keys)
            {
                extract.OnStatusChanged -= Paint;
                UnhookSwitches(extract);
            }

            DropMarkers();
        }

        private void DropMarkers()
        {
            foreach (var marker in _byMarker.Keys)
            {
                marker.ContainingMapView?.RemoveMapMarker(marker);
            }

            _byPoint.Clear();
            _byMarker.Clear();
        }

        private void Paint(ExfiltrationPoint extract, EExfiltrationStatus status)
        {
            if (!_byPoint.TryGetValue(extract, out var marker) || !_byMarker.TryGetValue(marker, out var points))
            {
                return;
            }

            var best = EExfiltrationStatus.NotPresent;
            var bestRank = -1;
            foreach (var point in points)
            {
                var rank = Rank(point.Status);
                if (rank <= bestRank)
                {
                    continue;
                }

                bestRank = rank;
                best = point.Status;
            }

            marker.SetDescription(Describe(points, best));
            if (!_statusTint)
            {
                marker.Color = Settings.ExtractDefaultColor.Value;
                return;
            }

            marker.Color = best switch
            {
                EExfiltrationStatus.NotPresent => Settings.ExtractClosedColor.Value,
                EExfiltrationStatus.UncompleteRequirements => Settings.ExtractHasRequirementsColor.Value,
                _ => Settings.ExtractOpenColor.Value
            };
        }

        private static int Rank(EExfiltrationStatus status) => status switch
        {
            EExfiltrationStatus.NotPresent => 0,
            EExfiltrationStatus.UncompleteRequirements => 1,
            _ => 2
        };

        private static string Describe(List<ExfiltrationPoint> points, EExfiltrationStatus best)
        {
            foreach (var point in points)
            {
                if (point.Status != best)
                {
                    continue;
                }

                var tip = ExtractRequirementText.Describe(point);
                if (!string.IsNullOrEmpty(tip))
                {
                    return tip;
                }
            }

            return "";
        }

        private void HookSwitches(ExfiltrationPoint extract)
        {
            if (extract.Switch == null || _switches.ContainsKey(extract))
            {
                return;
            }

            WorldInteractiveObjectInteract handler = (_, _, _) => Paint(extract, extract.Status);
            _switches[extract] = handler;
            var seen = new HashSet<Switch>();
            var current = extract.Switch;
            while (current != null && seen.Add(current))
            {
                current.OnDoorStateChanged += handler;
                current = current.PreviousSwitch;
            }
        }

        private void UnhookSwitches(ExfiltrationPoint extract)
        {
            if (!_switches.TryGetValue(extract, out var handler))
            {
                return;
            }

            _switches.Remove(extract);
            if (extract.Switch == null)
            {
                return;
            }

            var seen = new HashSet<Switch>();
            var current = extract.Switch;
            while (current != null && seen.Add(current))
            {
                current.OnDoorStateChanged -= handler;
                current = current.PreviousSwitch;
            }
        }
    }
}
