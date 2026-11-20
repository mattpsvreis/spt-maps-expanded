using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace MapsExtended;

[Injectable]
public class CustomStaticRouter : StaticRouter
{
    private static ServerMapPolicy? _mapPolicy;

    public CustomStaticRouter(
        JsonUtil jsonUtil) : base(
        jsonUtil,
        GetCustomRoutes()
    )
    {}

    public void PassConfig(ServerMapPolicy config)
    {
        _mapPolicy = config;
    }

    private static List<RouteAction> GetCustomRoutes()
    {
        return
        [
            new RouteAction<EmptyRequestData>(
                "/mapsexpanded/load",
                async (
                    url,
                    info,
                    sessionId,
                    output,
                    token
                ) => await HandleRoute(url, info, sessionId)
            )
        ];
    }

    private static ValueTask<string> HandleRoute(string url, EmptyRequestData info, MongoId sessionId)
    {
        return new ValueTask<string>(JsonSerializer.Serialize(_mapPolicy));
    }
}