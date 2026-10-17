namespace Toushir.Wholesale;

public static class RouteEndpoints
{
    public static void MapRouteOperations(this WebApplication app, RouteGroupBuilder secure)
    {
        var operations = secure.MapGroup("/operations/routes");
        operations.MapGet("", async (RouteService service, HttpContext context) => Results.Ok(await service.List(Actor.From(context))));
        operations.MapGet("/{id}", async (string id, RouteService service, HttpContext context) => Results.Ok(await service.Get(Actor.From(context), id)));
        operations.MapPost("", async (CreateRouteInput input, RouteService service, HttpContext context) => Results.Ok(await service.Create(Actor.From(context), input)));
        operations.MapPost("/{id}/reorder", async (string id, ReorderRouteInput input, RouteService service, HttpContext context) => Results.Ok(await service.Reorder(Actor.From(context),id,input)));
        operations.MapPost("/{id}/manifest-challenge", async (string id, VersionInput input, RouteService service, HttpContext context) => Results.Ok(await service.ManifestChallenge(Actor.From(context), id, input)));
        operations.MapPost("/{id}/returns", async (string id,DepotReturnInput input,RouteService service,HttpContext context)=>Results.Ok(await service.ReceiveReturns(Actor.From(context),id,input)));
        operations.MapPost("/{id}/cash-count", async (string id, CountCashInput input, RouteService service, HttpContext context) => Results.Ok(await service.CountCash(Actor.From(context), id, input)));
        operations.MapPost("/{id}/reconcile", async (string id, ReconcileRouteInput input, RouteService service, HttpContext context) => Results.Ok(await service.Reconcile(Actor.From(context), id, input)));
        var courier = secure.MapGroup("/courier/routes");
        courier.MapGet("", async (RouteService service, HttpContext context) => { Actor.From(context).RequireRoles("DRIVER"); return Results.Ok(await service.List(Actor.From(context))); });
        courier.MapGet("/{id}", async (string id, RouteService service, HttpContext context) => { Actor.From(context).RequireRoles("DRIVER"); return Results.Ok(await service.Get(Actor.From(context), id)); });
        courier.MapPost("/{id}/accept-custody", async (string id, CustodyInput input, RouteService service, HttpContext context) => Results.Ok(await service.AcceptCustody(Actor.From(context), id, input)));
        courier.MapPost("/{id}/declare-cash", async (string id, DeclareCashInput input, RouteService service, HttpContext context) => Results.Ok(await service.DeclareCash(Actor.From(context), id, input)));
    }
}
