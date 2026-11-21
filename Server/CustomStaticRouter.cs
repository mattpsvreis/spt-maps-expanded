using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace MapsExtended;

[Injectable]
public class MapPolicyRouter : StaticRouter
{
    private static ServerMapPolicy? _policy;

    public MapPolicyRouter(JsonUtil jsonUtil) : base(jsonUtil, Routes())
    {
    }

    public void SetPolicy(ServerMapPolicy policy)
    {
        _policy = policy;
    }

    private static List<RouteAction> Routes()
    {
        return
        [
            new RouteAction<EmptyRequestData>(
                "/mapsexpanded/load",
                async (url, info, sessionId, output, token) => await Handle(url, info, sessionId))
        ];
    }

    private static ValueTask<string> Handle(string url, EmptyRequestData info, MongoId sessionId)
    {
        return new ValueTask<string>(JsonSerializer.Serialize(_policy));
    }
}
