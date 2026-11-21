using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SPT.Reflection.Utils;
using Comfort.Common;
using MapsExtended.Integration;
using EFT;
using EFT.InventoryLogic;
using EFT.Vehicle;
using HarmonyLib;

namespace MapsExtended.Integration
{
    public static class GameUtils
    {
        // reflection        
        private static readonly FieldInfo PlayerCorpseField = AccessTools.Field(typeof(Player), "Corpse");
        private static readonly FieldInfo PlayerLastAggressorField = AccessTools.Field(typeof(Player), "LastAggressor");
        
        private static readonly Type ProfileInterface = typeof(IEftSession).GetInterfaces().First(i =>
            {
                var properties = i.GetProperties();
                return properties.Length == 2 &&
                       properties.Any(p => p.Name == "Profile");
            });
        private static readonly PropertyInfo SessionProfileProperty = AccessTools.Property(ProfileInterface, "Profile");
        private static IEftSession Session
        {
            get => ClientAppUtils.GetMainApp().GetClientBackEndSession();
        }
        private static Profile PlayerProfile
        {
            get => SessionProfileProperty.GetValue(Session) as Profile;
        }

        private static readonly HashSet<WildSpawnType> TrackedBosses =
        [
            WildSpawnType.bossBoar,// Kaban
            WildSpawnType.bossBully,// Reshala
            WildSpawnType.bossGluhar,// Glukhar
            WildSpawnType.bossKilla,
            WildSpawnType.bossKnight,
            WildSpawnType.followerBigPipe,
            WildSpawnType.followerBirdEye,
            WildSpawnType.bossKolontay,
            WildSpawnType.bossKojaniy,// Shturman
            WildSpawnType.bossSanitar,
            WildSpawnType.bossTagilla,
            WildSpawnType.bossPartisan,
            WildSpawnType.bossZryachiy,
            WildSpawnType.followerZryachiy,
            WildSpawnType.gifter,// Santa
            WildSpawnType.arenaFighterEvent,// Blood Hounds
            WildSpawnType.sectantPriest,// Cultist Priest
            WildSpawnType.bossTagillaAgro,// Tagilla Labyrinth
            WildSpawnType.bossKillaAgro,// Killa Labyrinth
            WildSpawnType.tagillaHelperAgro,// Tagilla Helper Labyrinth
            WildSpawnType.peacefullZryachiyEvent,
            WildSpawnType.ravangeZryachiyEvent
        ];

        public static bool IsInRaid()
        {
            var game = Singleton<AbstractGame>.Instance;
            var botGame = Singleton<IBotGame>.Instance;

            return ((game != null) && game.InRaid)
                || ((botGame != null) && botGame.Status != GameStatus.Stopped
                                      && botGame.Status != GameStatus.Stopping
                                      && botGame.Status != GameStatus.SoftStopping);
        }

        public static string GetCurrentMapInternalName()
        {
            var gameWorld = Singleton<GameWorld>.Instance;
            return gameWorld?.MainPlayer?.Location;
        }
        
        public static Player GetMainPlayer()
        {
            var gameWorld = Singleton<GameWorld>.Instance;
            return gameWorld?.MainPlayer;
        }

        public static Profile GetPlayerProfile()
        {
            return PlayerProfile;
        }

        public static BTRView GetBtrView()
        {
            var gameWorld = Singleton<GameWorld>.Instance;
            return gameWorld?.BtrController?.BtrView;
        }

        public static bool IsScavRaid()
        {
            var player = GetMainPlayer();
            return IsInRaid() && (player != null) && player.Side == EPlayerSide.Savage;
        }

        public static string BsgLocalized(this MongoID id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "";
            }

            return id.ToString().Localized();
        }
        
        public static string BsgLocalized(this string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "";
            }

            return id.Localized();
        }

        public static bool IsGroupedWithMainPlayer(this IPlayer player)
        {
            var mainPlayerGroupId = GetMainPlayer().GroupId;
            return !string.IsNullOrEmpty(mainPlayerGroupId) && player.GroupId == mainPlayerGroupId;
        }

        public static bool IsNonVanillaBot(this IPlayer player)
        {
            return (int)player.Profile.Info.Settings.Role > 195;
        }

        public static bool IsTrackedBoss(this IPlayer player)
        {
            if (player?.Profile?.Info?.Settings == null)
            {
                return false;
            }

            if (player.Profile.Side == EPlayerSide.Savage && TrackedBosses.Contains(player.Profile.Info.Settings.Role))
            {
                return true;
            }

            // UNTAR leaders/officers use boss shapes + boss visibility (ShowBossMarkersInRaid)
            return FactionColours.IsUntarBossRole(player);
        }

        public static bool IsPmc(this IPlayer player)
        {
            return player.Profile.Side == EPlayerSide.Bear || player.Profile.Side == EPlayerSide.Usec;
        }

        public static bool IsScav(this IPlayer player)
        {
            return player.Profile.Side == EPlayerSide.Savage;
        }

        public static bool DidMainPlayerKill(this IPlayer player)
        {
            var aggressor = PlayerLastAggressorField.GetValue(player) as IPlayer;
            if (aggressor == null)
            {
                return false;
            }

            var mainPlayer = GetMainPlayer();
            if (aggressor.ProfileId == mainPlayer.ProfileId)
            {
                return true;
            }

            return false;
        }

        public static bool DidTeammateKill(this IPlayer player)
        {
            var aggressor = PlayerLastAggressorField.GetValue(player) as IPlayer;
            if (aggressor == null || string.IsNullOrEmpty(aggressor.GroupId))
            {
                return false;
            }

            var mainPlayer = GetMainPlayer();
            if (aggressor.ProfileId != mainPlayer.ProfileId && aggressor.GroupId == GetMainPlayer().GroupId)
            {
                return true;
            }

            return false;
        }

        public static bool IsBtrShooter(this IPlayer player)
        {
            return player.Profile.Side == EPlayerSide.Savage
                && player.Profile.Info.Settings.Role == WildSpawnType.shooterBTR;
        }

        public static bool HasCorpse(this Player player)
        {
            return PlayerCorpseField.GetValue(player) != null;
        }

        public static bool IsHeadlessClient(this IPlayer player)
        {
            return player.Profile.Info.MemberCategory == EMemberCategory.UnitTest;
        }

    }
}
