using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Hooks;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class OtherPlayersMarkerProvider : PinSource
    {
        private const string Arrow = "Markers/arrow.png";
        private const string Star = "Markers/star.png";
        private readonly Dictionary<Player, PlayerMapMarker> _pins = [];

        private bool _friendly = true;
        private bool _enemies;
        private bool _scavs;
        private bool _bosses;
        private bool _custom;

        public bool ShowFriendlyPlayers
        {
            get => _friendly;
            set => Toggle(ref _friendly, value);
        }

        public bool ShowEnemyPlayers
        {
            get => _enemies;
            set => Toggle(ref _enemies, value);
        }

        public bool ShowScavs
        {
            get => _scavs;
            set => Toggle(ref _scavs, value);
        }

        public bool ShowBosses
        {
            get => _bosses;
            set => Toggle(ref _bosses, value);
        }

        public bool ShowNonVanillaBots
        {
            get => _custom;
            set => Toggle(ref _custom, value);
        }

        protected override void ShowInRaid(MapCanvas map)
        {
            PlaceAll();
            DropDead();
            Singleton<GameWorld>.Instance.OnPersonAdd += Place;
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer += OnGone;
            PlayerOnDeadPatch.OnDead += Drop;
        }

        protected override void HideInRaid(MapCanvas map) => Unhook();

        protected override void EndRaid(MapCanvas map)
        {
            Unhook();
            Clear();
        }

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            foreach (var player in _pins.Keys.ToList())
            {
                Drop(player);
                Place(player);
            }
        }

        protected override void Disable(MapCanvas map)
        {
            Unhook();
            Clear();
        }

        public void RefreshMarkers()
        {
            if (!GameUtils.IsInRaid())
            {
                return;
            }

            foreach (var player in _pins.Keys.ToList())
            {
                if (player.IsYourPlayer)
                {
                    continue;
                }

                Drop(player);
                Place(player);
            }
        }

        private void PlaceAll()
        {
            if (!GameUtils.IsInRaid())
            {
                return;
            }

            foreach (var player in Singleton<GameWorld>.Instance.AllAlivePlayersList)
            {
                if (!player.IsYourPlayer && !_pins.ContainsKey(player))
                {
                    Place(player);
                }
            }
        }

        private void Place(IPlayer person)
        {
            if (person is not Player player || player.IsHeadlessClient() || Map == null
                || player.IsBtrShooter() || _pins.ContainsKey(player))
            {
                return;
            }

            var category = "";
            var icon = "";
            var color = Color.clear;
            if (player.IsGroupedWithMainPlayer())
            {
                category = "Friendly Player";
                icon = Arrow;
                color = Settings.FriendlyPlayerColor.Value;
            }
            else if (player.IsTrackedBoss())
            {
                category = "Boss";
                icon = Star;
                color = Settings.BossColor.Value;
            }
            else if (player.IsNonVanillaBot())
            {
                category = "Non-Vanilla Bot";
                icon = Settings.NonVanillaBotUseStar.Value ? Star : Arrow;
                color = Settings.NonVanillaBotColor.Value;
            }
            else if (player.IsPmc())
            {
                category = "Enemy Player";
                icon = Arrow;
                color = player.Side == EPlayerSide.Bear
                    ? Settings.PmcBearColor.Value
                    : Settings.PmcUsecColor.Value;
            }
            else if (player.IsScav())
            {
                category = "Scav";
                icon = Arrow;
                color = Settings.ScavColor.Value;
            }

            if (!Allowed(category))
            {
                return;
            }

            var marker = Map.AddPlayerMarker(player, category, color, icon);
            FactionColours.TryTintMarker(player, marker, color);
            _pins[player] = marker;
        }

        private void OnGone(IPlayer person)
        {
            if (person is Player player)
            {
                Drop(player);
            }
        }

        private void DropDead()
        {
            var alive = new HashSet<Player>(Singleton<GameWorld>.Instance.AllAlivePlayersList);
            foreach (var player in _pins.Keys.ToList())
            {
                if (player.HasCorpse() || !alive.Contains(player))
                {
                    Drop(player);
                }
            }
        }

        private void Drop(Player player)
        {
            if (!_pins.TryGetValue(player, out var marker))
            {
                return;
            }

            marker.ContainingMapView?.RemoveMapMarker(marker);
            _pins.Remove(player);
        }

        private void Clear()
        {
            foreach (var player in _pins.Keys.ToList())
            {
                Drop(player);
            }
        }

        private void DropDisabled()
        {
            foreach (var player in _pins.Keys.ToList())
            {
                if (!Allowed(_pins[player].Category))
                {
                    Drop(player);
                }
            }
        }

        private bool Allowed(string category) => category switch
        {
            "Friendly Player" => _friendly,
            "Enemy Player" => _enemies,
            "Boss" => _bosses,
            "Non-Vanilla Bot" => _custom,
            "Scav" => _scavs,
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

        private void Unhook()
        {
            var world = Singleton<GameWorld>.Instance;
            if (world != null)
            {
                world.OnPersonAdd -= Place;
            }

            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnGone;
            PlayerOnDeadPatch.OnDead -= Drop;
        }
    }
}
