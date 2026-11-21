using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace MapsExtended;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.dark.mapsexpanded";
    public string Name { get; init; } = "Maps Expanded";
    public string Author { get; init; } = "Dark";
    public List<string>? Contributors { get; init; } = ["mpstark", "dirtbikercj", "acidphantasm", "BLACK HAWK"];
    public SemanticVersioning.Version Version { get; init; } = new("2.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; } =
    [
        "com.mpstark.dynamicmaps",
        "com.blackhawk.dynamicmapsextended",
        "com.dark.dynamicmapsplus"
    ];
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public bool HasPrepatcher { get; init; }
    public string License { get; init; } = "MIT";
}

/// <summary>
/// Server-driven policy for which player/bot marker categories are allowed in raid.
/// Serialized to clients via /mapsexpanded/load. Property names must stay stable.
/// </summary>
public class ServerMapPolicy
{
    public bool AllowShowFriendlyPlayerMarkersInRaid { get; set; }
    public bool AllowShowEnemyPlayerMarkersInRaid { get; set; }
    public bool AllowShowBossMarkersInRaid { get; set; }
    public bool AllowShowScavMarkersInRaid { get; set; }
    public bool AllowShowNonVanillaBotMarkersInRaid { get; set; }
}

[Injectable(TypePriority = OnLoadOrder.Preload)]
public class MapsExtendedServer(
    ModHelper modHelper,
    MapPolicyRouter mapPolicyRouter)
    : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var policy = modHelper.GetJsonDataFromFile<ServerMapPolicy>(pathToMod, "config.json");
        mapPolicyRouter.SetPolicy(policy);
        return Task.CompletedTask;
    }
}
