namespace Toushir.Wholesale;

public static class WarehouseEndpoints
{
    public static IServiceCollection AddWarehouseModule(this IServiceCollection services)
    {
        services.AddScoped<WarehouseService>();
        return services;
    }
    public static WebApplication MapWarehouseEndpoints(this WebApplication app, RouteGroupBuilder secure)
    {
        var api = secure.MapGroup("/admin/warehouse");
        api.MapGet("/lots", async (string? productId, WarehouseService s, HttpContext c) => Results.Ok(await s.Lots(Actor.From(c), productId)));
        api.MapPost("/activate", async (ActivateLotsInput x, WarehouseService s, HttpContext c) => Results.Ok(await s.Activate(Actor.From(c), x)));
        api.MapPost("/lots/{id}/count", async (string id, CountLotInput x, WarehouseService s, HttpContext c) => Results.Ok(await s.CountLot(Actor.From(c), id, x)));
        api.MapPost("/lots/{id}/quality", async (string id, LotQualityInput x, WarehouseService s, HttpContext c) => Results.Ok(await s.Quality(Actor.From(c), id, x)));
        api.MapGet("/waves", async (WarehouseService s, HttpContext c) => Results.Ok(await s.Waves(Actor.From(c))));
        api.MapPost("/waves", async (CreatePickWaveInput x, WarehouseService s, HttpContext c) => Results.Ok(await s.CreateWave(Actor.From(c), x)));
        api.MapGet("/waves/{id}", async (string id, WarehouseService s, HttpContext c) => Results.Ok(await s.Wave(Actor.From(c), id)));
        api.MapPost("/tasks/{id}/scan", async (string id, ScanPickInput x, WarehouseService s, HttpContext c) => Results.Ok(await s.Scan(Actor.From(c), id, x)));
        api.MapPost("/returns/identify", async (IdentifyReturnLotInput x, WarehouseService s, HttpContext c) => Results.Ok(await s.IdentifyReturn(Actor.From(c), x)));
        return app;
    }
}
