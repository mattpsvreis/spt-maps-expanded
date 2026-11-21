using System;
using EFT;
using EFT.Interactive;

namespace MapsExtended.App
{
    /// <summary>
    /// Raid-side signals raised by Harmony patches. Pin sources subscribe here.
    /// </summary>
    internal static class RaidEvents
    {
        internal static event Action RaidEnded;
        internal static event Action<IPlayer> PlayerUnregistered;
        internal static event Action<LootItem> LootRegistered;
        internal static event Action<LootItem> LootDestroyed;
        internal static event Action<Player> PlayerDied;

        internal static void RaiseRaidEnded()
        {
            var handlers = RaidEnded?.GetInvocationList();
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Plugin.Log?.LogError($"Raid end handler {handler.Method.DeclaringType?.FullName}.{handler.Method.Name} failed: {e}");
                }
            }
        }

        internal static void RaisePlayerUnregistered(IPlayer player) => PlayerUnregistered?.Invoke(player);

        internal static void RaiseLootRegistered(LootItem loot) => LootRegistered?.Invoke(loot);

        internal static void RaiseLootDestroyed(LootItem loot) => LootDestroyed?.Invoke(loot);

        internal static void RaisePlayerDied(Player player) => PlayerDied?.Invoke(player);
    }
}
