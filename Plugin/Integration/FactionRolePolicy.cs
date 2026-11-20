using System;

namespace MapsExtended.Integration
{
    /// <summary>
    /// Maps known custom WildSpawnType role names (and MoreBotsAPI scav-role metadata)
    /// onto faction colour groups. Exact registered names only — no nickname guessing.
    /// </summary>
    internal static class FactionRolePolicy
    {
        internal static bool IsUntarBoss(string role) =>
            string.Equals(role, "bossuntarlead", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "bossuntarofficer", StringComparison.OrdinalIgnoreCase);

        internal static string Resolve(string role, string faction)
        {
            switch ((role ?? "").ToLowerInvariant())
            {
                case "isbteamleader":
                case "isbsecondleader":
                case "isbscout":
                case "isbheavy":
                case "isbassassin":
                case "isbmarksman":
                case "isbspecialforces":
                case "isbbosscommander":
                case "isbfollowercommander":
                    return "ISB";
                case "isbbossfirefly":
                case "isbfollowervipper":
                case "isbfollowerloghan":
                case "isbfollowerspearthrower":
                case "isbfollowerzofia":
                case "isbfollowerevernight":
                case "isbfollowernemesis":
                    return "White Tusks";
                case "blackdivlead":
                case "blackdivassault":
                case "blackdivbreacher":
                case "blackdivsupport":
                case "blackdivib":
                case "bosswedge":
                    return "Black Division";
                case "followeruntar":
                case "bossuntarlead":
                case "followeruntarmarksman":
                case "bossuntarofficer":
                    return "UNTAR";
                case "ruafrifleman":
                case "ruafriflemansenior":
                case "ruafautorifleman":
                case "ruafgrenadier":
                case "ruafmarksman":
                case "ruafmachinegunner":
                    return "RUAF";
                case "remnantrifleman":
                    return "Remnant";
            }

            switch ((faction ?? "").Trim().ToLowerInvariant())
            {
                case "isbwhitetusk":
                    return "White Tusks";
                case "isb":
                case "isbboss":
                case "isbguard":
                case "isbscout":
                case "isbheavy":
                case "isbassassin":
                case "isbsniper":
                case "isbcommander":
                case "isbbossfollower":
                    return "ISB";
                case "blackdiv":
                case "black division":
                    return "Black Division";
                case "untar":
                    return "UNTAR";
                case "ruaf":
                    return "RUAF";
                case "remnant":
                    return "Remnant";
                default:
                    return null;
            }
        }
    }
}
