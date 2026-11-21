using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Vehicle;
using HarmonyLib;
using SPT.Reflection.Utils;

namespace MapsExtended.Integration
{
    public static class GameUtils
    {
        private const int VanillaRoleCeiling = 195;

        private static readonly FieldInfo CorpseField = AccessTools.Field(typeof(Player), "Corpse");
        private static readonly FieldInfo LastAggressorField = AccessTools.Field(typeof(Player), "LastAggressor");
        private static readonly PropertyInfo SessionProfile = FindSessionProfileProperty();

        private static readonly HashSet<WildSpawnType> BossRoles = new HashSet<WildSpawnType>
        {
            WildSpawnType.bossBoar,
            WildSpawnType.bossBully,
            WildSpawnType.bossGluhar,
            WildSpawnType.bossKilla,
            WildSpawnType.bossKnight,
            WildSpawnType.followerBigPipe,
            WildSpawnType.followerBirdEye,
            WildSpawnType.bossKolontay,
            WildSpawnType.bossKojaniy,
            WildSpawnType.bossSanitar,
            WildSpawnType.bossTagilla,
            WildSpawnType.bossPartisan,
            WildSpawnType.bossZryachiy,
            WildSpawnType.followerZryachiy,
            WildSpawnType.gifter,
            WildSpawnType.arenaFighterEvent,
            WildSpawnType.sectantPriest,
            WildSpawnType.bossTagillaAgro,
            WildSpawnType.bossKillaAgro,
            WildSpawnType.tagillaHelperAgro,
            WildSpawnType.peacefullZryachiyEvent,
            WildSpawnType.ravangeZryachiyEvent
        };

        public static bool IsInRaid()
        {
            var game = Singleton<AbstractGame>.Instance;
            if (game != null && game.InRaid)
            {
                return true;
            }

            var botGame = Singleton<IBotGame>.Instance;
            if (botGame == null)
            {
                return false;
            }

            var status = botGame.Status;
            return status != GameStatus.Stopped
                && status != GameStatus.Stopping
                && status != GameStatus.SoftStopping;
        }

        public static string GetCurrentMapInternalName()
        {
            return Singleton<GameWorld>.Instance?.MainPlayer?.Location;
        }

        public static Player GetMainPlayer()
        {
            return Singleton<GameWorld>.Instance?.MainPlayer;
        }

        public static Profile GetPlayerProfile()
        {
            var session = ClientAppUtils.GetMainApp()?.GetClientBackEndSession();
            if (session == null || SessionProfile == null)
            {
                return null;
            }

            return SessionProfile.GetValue(session) as Profile;
        }

        public static BTRView GetBtrView()
        {
            return Singleton<GameWorld>.Instance?.BtrController?.BtrView;
        }

        public static bool IsScavRaid()
        {
            var player = GetMainPlayer();
            return IsInRaid() && player != null && player.Side == EPlayerSide.Savage;
        }

        public static string BsgLocalized(this MongoID id)
        {
            return BsgLocalized(id.ToString());
        }

        public static string BsgLocalized(this string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            return id.Localized();
        }

        public static bool IsGroupedWithMainPlayer(this IPlayer player)
        {
            var groupId = GetMainPlayer()?.GroupId;
            return !string.IsNullOrEmpty(groupId) && player != null && player.GroupId == groupId;
        }

        public static bool IsNonVanillaBot(this IPlayer player)
        {
            return player?.Profile?.Info?.Settings != null
                && (int)player.Profile.Info.Settings.Role > VanillaRoleCeiling;
        }

        public static bool IsTrackedBoss(this IPlayer player)
        {
            var settings = player?.Profile?.Info?.Settings;
            if (settings == null)
            {
                return false;
            }

            if (player.Profile.Side == EPlayerSide.Savage && BossRoles.Contains(settings.Role))
            {
                return true;
            }

            return FactionColours.IsUntarBossRole(player);
        }

        public static bool IsPmc(this IPlayer player)
        {
            var side = player?.Profile?.Side;
            return side == EPlayerSide.Bear || side == EPlayerSide.Usec;
        }

        public static bool IsScav(this IPlayer player)
        {
            return player?.Profile?.Side == EPlayerSide.Savage;
        }

        public static bool DidMainPlayerKill(this IPlayer player)
        {
            var killer = ReadAggressor(player);
            var main = GetMainPlayer();
            return killer != null && main != null && killer.ProfileId == main.ProfileId;
        }

        public static bool DidTeammateKill(this IPlayer player)
        {
            var killer = ReadAggressor(player);
            var main = GetMainPlayer();
            if (killer == null || main == null || string.IsNullOrEmpty(killer.GroupId))
            {
                return false;
            }

            return killer.ProfileId != main.ProfileId && killer.GroupId == main.GroupId;
        }

        public static bool IsBtrShooter(this IPlayer player)
        {
            return player?.Profile?.Side == EPlayerSide.Savage
                && player.Profile.Info.Settings.Role == WildSpawnType.shooterBTR;
        }

        public static bool HasCorpse(this Player player)
        {
            return player != null && CorpseField != null && CorpseField.GetValue(player) != null;
        }

        public static bool IsHeadlessClient(this IPlayer player)
        {
            return player?.Profile?.Info != null
                && player.Profile.Info.MemberCategory == EMemberCategory.UnitTest;
        }

        private static IPlayer ReadAggressor(IPlayer player)
        {
            if (player == null || LastAggressorField == null)
            {
                return null;
            }

            return LastAggressorField.GetValue(player) as IPlayer;
        }

        private static PropertyInfo FindSessionProfileProperty()
        {
            foreach (var contract in typeof(IEftSession).GetInterfaces())
            {
                var properties = contract.GetProperties();
                if (properties.Length != 2)
                {
                    continue;
                }

                foreach (var property in properties)
                {
                    if (property.Name == "Profile")
                    {
                        return property;
                    }
                }
            }

            return null;
        }
    }
}
