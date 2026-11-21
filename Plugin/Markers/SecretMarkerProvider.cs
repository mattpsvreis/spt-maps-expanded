using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT;
using EFT.Interactive;
using EFT.Interactive.SecretExfiltrations;

namespace MapsExtended.Markers
{
    public class SecretMarkerProvider : PinSource
    {
        private const string Icon = "Markers/exit.png";
        private readonly PinBag<SecretExfiltrationPoint> _pins = new();
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
                foreach (var point in _pins.Keys)
                {
                    Paint(point, point.Status);
                }
            }
        }

        protected override void ShowInRaid(MapCanvas map)
        {
            if (_pins.Count == 0)
            {
                foreach (var point in Singleton<GameWorld>.Instance.ExfiltrationController.SecretExfiltrationPoints)
                {
                    Place(point);
                }
            }

            foreach (var point in _pins.Keys)
            {
                Paint(point, point.Status);
                point.OnStatusChanged += Paint;
            }
        }

        protected override void HideInRaid(MapCanvas map)
        {
            foreach (var point in _pins.Keys)
            {
                point.OnStatusChanged -= Paint;
            }
        }

        protected override void EndRaid(MapCanvas map) => Clear();

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            foreach (var point in _pins.Keys.ToList())
            {
                Drop(point);
                Place(point);
            }
        }

        protected override void Disable(MapCanvas map) => Clear();

        protected override void ColorsChanged(MapCanvas map)
        {
            foreach (var point in _pins.Keys)
            {
                Paint(point, point.Status);
            }
        }

        private void Place(SecretExfiltrationPoint point)
        {
            if (Map == null || point == null || _pins.Has(point))
            {
                return;
            }

            _pins.Put(point, Map.AddMapMarker(new MapMarkerDef
            {
                Category = "Secret",
                ImagePath = Icon,
                Text = point.Settings.Name.BsgLocalized(),
                Position = MathUtils.ConvertToMapPosition(point.transform),
                Color = Settings.SecretPointColor.Value
            }));
            Paint(point, point.Status);
        }

        private void Drop(SecretExfiltrationPoint point)
        {
            if (!_pins.Has(point))
            {
                return;
            }

            point.OnStatusChanged -= Paint;
            _pins.Drop(point);
        }

        private void Clear()
        {
            foreach (var point in _pins.Keys.ToList())
            {
                Drop(point);
            }
        }

        private void Paint(ExfiltrationPoint point, EExfiltrationStatus status)
        {
            if (point is not SecretExfiltrationPoint secret || !_pins.TryGet(secret, out var marker))
            {
                return;
            }

            marker.SetDescription(ExtractRequirementText.Describe(point));
            if (!_statusTint)
            {
                marker.Color = Settings.SecretPointColor.Value;
                return;
            }

            marker.Color = status switch
            {
                EExfiltrationStatus.Hidden => Settings.SecretPointColor.Value,
                EExfiltrationStatus.UncompleteRequirements => Settings.ExtractHasRequirementsColor.Value,
                EExfiltrationStatus.Countdown => Settings.ExtractOpenColor.Value,
                _ => Settings.SecretPointColor.Value
            };
        }
    }
}
