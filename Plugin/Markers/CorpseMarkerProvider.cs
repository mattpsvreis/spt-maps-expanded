using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Hooks;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT;

namespace MapsExtended.Markers
{
    public class CorpseMarkerProvider : PinSource
    {
        private const string Icon = "Markers/skull.png";
        private readonly PinBag<Player> _pins = new();

        private bool _friendly = true;
        private bool _killed = true;
        private bool _friendlyKilled = true;
        private bool _bosses;
        private bool _others;

        public bool ShowFriendlyCorpses
        {
            get => _friendly;
            set => Toggle(ref _friendly, value);
        }

        public bool ShowKilledCorpses
        {
            get => _killed;
            set => Toggle(ref _killed, value);
        }

        public bool ShowFriendlyKilledCorpses
        {
            get => _friendlyKilled;
            set => Toggle(ref _friendlyKilled, value);
        }

        public bool ShowBossCorpses
        {
            get => _bosses;
            set => Toggle(ref _bosses, value);
        }

        public bool ShowOtherCorpses
        {
            get => _others;
            set => Toggle(ref _others, value);
        }

        protected override void ShowInRaid(MapCanvas map)
        {
            PlaceAll();
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer += OnGone;
        }

        protected override void HideInRaid(MapCanvas map)
        {
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnGone;
        }

        protected override void EndRaid(MapCanvas map)
        {
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnGone;
            _pins.Clear();
        }

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            foreach (var corpse in _pins.Keys.ToList())
            {
                _pins.Drop(corpse);
                Place(corpse);
            }
        }

        protected override void Disable(MapCanvas map)
        {
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnGone;
            _pins.Clear();
        }

        public void RefreshMarkers()
        {
            if (!GameUtils.IsInRaid())
            {
                return;
            }

            _pins.Clear();
            PlaceAll();
        }

        private void OnGone(IPlayer person)
        {
            if (person is Player player && player.HasCorpse())
            {
                Place(player);
            }
        }

        private void PlaceAll()
        {
            if (!GameUtils.IsInRaid())
            {
                return;
            }

            foreach (var player in Singleton<GameWorld>.Instance.AllPlayersEverExisted)
            {
                if (player.HasCorpse() && !_pins.Has(player))
                {
                    Place(player);
                }
            }
        }

        private void Place(Player player)
        {
            if (Map == null || player == null || _pins.Has(player))
            {
                return;
            }

            var category = "Other Corpse";
            var color = Settings.KilledOtherColor.Value;
            if (player.IsGroupedWithMainPlayer())
            {
                category = "Friendly Corpse";
                color = Settings.FriendlyCorpseColor.Value;
            }
            else if (player.IsTrackedBoss() && player.DidMainPlayerKill())
            {
                category = "Killed Boss Corpse";
                color = Settings.KilledBossColor.Value;
            }
            else if (player.DidMainPlayerKill())
            {
                category = "Killed Corpse";
                color = Settings.KilledCorpseColor.Value;
            }
            else if (player.IsTrackedBoss() && player.DidTeammateKill())
            {
                category = "Friendly Killed Boss Corpse";
                color = Settings.KilledBossColor.Value;
            }
            else if (player.DidTeammateKill())
            {
                category = "Friendly Killed Corpse";
                color = Settings.FriendlyKilledCorpseColor.Value;
            }
            else if (player.IsTrackedBoss())
            {
                category = "Boss Corpse";
                color = Settings.KilledBossColor.Value;
            }

            if (!Allowed(category))
            {
                return;
            }

            _pins.Put(player, Map.AddMapMarker(new MapMarkerDef
            {
                Category = category,
                ImagePath = Icon,
                Text = player.Profile.GetCorrectedNickname(),
                Color = color,
                Position = MathUtils.ConvertToMapPosition(((IPlayer)player).Position)
            }));
        }

        private void DropDisabled()
        {
            foreach (var corpse in _pins.Keys.ToList())
            {
                if (_pins.TryGet(corpse, out var marker) && !Allowed(marker.Category))
                {
                    _pins.Drop(corpse);
                }
            }
        }

        private bool Allowed(string category) => category switch
        {
            "Friendly Corpse" => _friendly,
            "Killed Corpse" or "Killed Boss Corpse" => _killed,
            "Friendly Killed Corpse" or "Friendly Killed Boss Corpse" => _friendlyKilled,
            "Boss Corpse" => _bosses,
            "Other Corpse" => _others,
            _ => false
        };

        private void Toggle(ref bool option, bool value)
        {
            if (option == value)
            {
                return;
            }

            option = value;
            if (option)
            {
                PlaceAll();
            }
            else
            {
                DropDisabled();
            }
        }
    }
}
