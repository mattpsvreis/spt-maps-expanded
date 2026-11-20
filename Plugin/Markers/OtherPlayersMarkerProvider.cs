using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Hooks;
using MapsExtended.Presentation.Overlays;
using MapsExtended.Presentation;
using EFT;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class OtherPlayersMarkerProvider : MarkerProviderBase
    {
        private const string ArrowImagePath = "Markers/arrow.png";
        private const string StarImagePath = "Markers/star.png";
        
        private const string FriendlyPlayerCategory = "Friendly Player";
        private const string FriendlyPlayerImagePath = ArrowImagePath;
        private static readonly Color FriendlyPlayerColor = Color.Lerp(Color.blue, Color.white, 0.5f);

        private const string EnemyPlayerCategory = "Enemy Player";
        private const string EnemyPlayerImagePath = ArrowImagePath;

        private const string ScavCategory = "Scav";
        private const string ScavImagePath = ArrowImagePath;

        private const string BossCategory = "Boss";
        private const string BossImagePath = StarImagePath;
        private const string NonVanillaBotCategory = "Non-Vanilla Bot";
        
        private bool _showFriendlyPlayers = true;
        public bool ShowFriendlyPlayers
        {
            get => _showFriendlyPlayers;
            set => HandleSetBoolOption(ref _showFriendlyPlayers, value);
        }

        private bool _showEnemyPlayers;
        public bool ShowEnemyPlayers
        {
            get => _showEnemyPlayers;
            set => HandleSetBoolOption(ref _showEnemyPlayers, value);
        }

        private bool _showScavs;
        public bool ShowScavs
        {
            get => _showScavs;
            set => HandleSetBoolOption(ref _showScavs, value);
        }

        private bool _showBosses;
        public bool ShowBosses
        {
            get => _showBosses;
            set => HandleSetBoolOption(ref _showBosses, value);
        }

        private bool _showNonVanillaBots;
        public bool ShowNonVanillaBots
        {
            get => _showNonVanillaBots;
            set => HandleSetBoolOption(ref _showNonVanillaBots, value);
        }

        private MapView _lastMapView;
        private readonly Dictionary<Player, PlayerMapMarker> _playerMarkers = [];

        public override void OnShowInRaid(MapView map)
        {
            _lastMapView = map;
            
            TryAddMarkers();
            RemoveNonActivePlayers();

            // register to event to get all the new ones while map is showing
            Singleton<GameWorld>.Instance.OnPersonAdd += TryAddMarker;

            // register to events to get when players die
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer += OnUnregisterPlayer;
            PlayerOnDeadPatch.OnDead += TryRemoveMarker;
        }

        public override void OnHideInRaid(MapView map)
        {
            // unregister from events while map isn't showing
            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld is not null)
            {
                gameWorld.OnPersonAdd -= TryAddMarker;
            }
            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnUnregisterPlayer;
            PlayerOnDeadPatch.OnDead -= TryRemoveMarker;
        }

        public override void OnRaidEnd(MapView map)
        {
            // unregister from events since map is ending
            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld is not null)
            {
                gameWorld.OnPersonAdd -= TryAddMarker;
            }

            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnUnregisterPlayer;
            PlayerOnDeadPatch.OnDead -= TryRemoveMarker;

            TryRemoveMarkers();
        }

        public override void OnMapChanged(MapView map, MapDef mapDef)
        {
            _lastMapView = map;

            foreach (var player in _playerMarkers.Keys.ToList())
            {
                TryRemoveMarker(player);
                TryAddMarker(player);
            }
        }

        public override void OnDisable(MapView map)
        {
            // unregister from events since provider is being disabled
            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld is not null)
            {
                gameWorld.OnPersonAdd -= TryAddMarker;
            }

            GameWorldUnregisterPlayerPatch.OnUnregisterPlayer -= OnUnregisterPlayer;
            PlayerOnDeadPatch.OnDead -= TryRemoveMarker;

            TryRemoveMarkers();
        }

        private void TryRemoveMarkers()
        {
            foreach (var player in _playerMarkers.Keys.ToList())
            {
                TryRemoveMarker(player);
            }

            _playerMarkers.Clear();
        }

        private void TryAddMarkers()
        {
            if (!GameUtils.IsInRaid())
            {
                return;
            }
            
            // add all players that have spawned already in raid
            var gameWorld = Singleton<GameWorld>.Instance;
            foreach (var player in gameWorld.AllAlivePlayersList)
            {
                if (player.IsYourPlayer || _playerMarkers.ContainsKey(player))
                {
                    continue;
                }

                TryAddMarker(player);
            }
        }

        private void OnUnregisterPlayer(IPlayer iPlayer)
        {
            var player = iPlayer as Player;
            if (player is null)
            {
                return;
            }

            TryRemoveMarker(player);
        }

        private void RemoveNonActivePlayers()
        {
            var alivePlayers = new HashSet<Player>(Singleton<GameWorld>.Instance.AllAlivePlayersList);
            foreach (var player in _playerMarkers.Keys.ToList())
            {
                if (player.HasCorpse() || !alivePlayers.Contains(player))
                {
                    TryRemoveMarker(player);
                }
            }
        }

        public void RefreshMarkers()
        {
            if (!GameUtils.IsInRaid()) return;

            foreach (var player in _playerMarkers.ToArray())
            {
                if (player.Key.IsYourPlayer) continue;
                
                TryRemoveMarker(player.Key);
                TryAddMarker(player.Key);
            }
        }
        
        private void TryAddMarker(IPlayer iPlayer)
        {
            var player = iPlayer as Player;
            if (player is null || player.IsHeadlessClient())
            {
                return;
            }

            if (_lastMapView is null || player.IsBtrShooter() || _playerMarkers.ContainsKey(player))
            {
                return;
            }
            
            // set category and color
            var category = string.Empty;
            var imagePath = string.Empty;
            var color = Color.clear;
            
            var intelLevel = GameUtils.GetIntelLevel();
            
            if (player.IsGroupedWithMainPlayer() && Settings.ShowFriendlyIntelLevel.Value <= intelLevel)
            {
                category = FriendlyPlayerCategory;
                imagePath = FriendlyPlayerImagePath;
                color = FriendlyPlayerColor;
            }
            // Bosses (including UNTAR leads via FactionColours) before NonVanillaBot —
            // custom bosses are usually role>195 and would otherwise lose star/boss visibility.
            else if (player.IsTrackedBoss() && Settings.ShowBossIntelLevel.Value <= intelLevel)
            {
                category = BossCategory;
                imagePath = BossImagePath;
                color = Settings.BossColor.Value;
            }
            else if (player.IsNonVanillaBot() && Settings.ShowScavIntelLevel.Value <= intelLevel)
            {
                category = NonVanillaBotCategory;
                imagePath = Settings.NonVanillaBotUseStar.Value ? StarImagePath : ArrowImagePath;
                color = Settings.NonVanillaBotColor.Value;
            }
            else if (player.IsPmc() && Settings.ShowPmcIntelLevel.Value <= intelLevel)
            {
                category = EnemyPlayerCategory;
                imagePath = EnemyPlayerImagePath;
                color = player.Side == EPlayerSide.Bear
                    ? Settings.PmcBearColor.Value
                    : Settings.PmcUsecColor.Value;
                    
            }
            else if (player.IsScav() && Settings.ShowScavIntelLevel.Value <= intelLevel)
            {
                category = ScavCategory;
                imagePath = ScavImagePath;
                color = Settings.ScavColor.Value;
            }

            if (!ShouldShowCategory(category))
            {
                return;
            }

            // try adding marker
            var marker = _lastMapView.AddPlayerMarker(player, category, color, imagePath);
            FactionColours.TryTintMarker(player, marker, color);
            _playerMarkers[player] = marker;
        }

        private void RemoveDisabledMarkers()
        {
            foreach (var player in _playerMarkers.Keys.ToList())
            {
                var marker = _playerMarkers[player];
                if (!ShouldShowCategory(marker.Category))
                {
                    TryRemoveMarker(player);
                }
            }
        }

        private void TryRemoveMarker(Player player)
        {
            if (!_playerMarkers.ContainsKey(player))
            {
                return;
            }

            _playerMarkers[player].ContainingMapView.RemoveMapMarker(_playerMarkers[player]);
            _playerMarkers.Remove(player);
        }

        private bool ShouldShowCategory(string category)
        {
            switch (category)
            {
                case FriendlyPlayerCategory:
                    return _showFriendlyPlayers;
                case EnemyPlayerCategory:
                    return _showEnemyPlayers;
                case BossCategory:
                    return _showBosses;
                case NonVanillaBotCategory:
                    return _showNonVanillaBots;
                case ScavCategory:
                    return _showScavs;
                default:
                    return false;
            }
        }

        private void HandleSetBoolOption(ref bool boolOption, bool value)
        {
            if (value == boolOption)
            {
                return;
            }

            boolOption = value;

            if (boolOption)
            {
                TryAddMarkers();
            }
            else
            {
                RemoveDisabledMarkers();
            }
        }
}
}
