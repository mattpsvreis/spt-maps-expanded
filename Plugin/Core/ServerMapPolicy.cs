namespace MapsExtended.Core
{
    /// <summary>
    /// Server-driven policy for which player/bot marker categories are allowed in raid.
    /// </summary>
    public class ServerMapPolicy
    {
        public bool AllowShowFriendlyPlayerMarkersInRaid;
        public bool AllowShowEnemyPlayerMarkersInRaid;
        public bool AllowShowBossMarkersInRaid;
        public bool AllowShowScavMarkersInRaid;
        public bool AllowShowNonVanillaBotMarkersInRaid;
    }
}
