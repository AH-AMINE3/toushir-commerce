namespace Toushir.Wholesale;

public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this RouteGroupBuilder secure)
    {
        var pricing = secure.MapGroup("/admin/pricing");
        pricing.MapGet("", async (PricingService service, HttpContext context)
            => Results.Ok(await service.Overview(Actor.From(context))));
        pricing.MapPost("/segments", async (PriceSegmentInput input, PricingService service, HttpContext context)
            => Results.Ok(await service.CreateSegment(Actor.From(context), input)));
        pricing.MapPost("/assignments/{merchantId}", async (string merchantId, AssignPriceSegmentInput input, PricingService service, HttpContext context)
            => Results.Ok(await service.AssignSegment(Actor.From(context), merchantId, input)));
        pricing.MapPost("/rules", async (TariffRuleInput input, PricingService service, HttpContext context)
            => Results.Ok(await service.CreateRule(Actor.From(context), input)));
        pricing.MapPost("/rules/{id}/review", async (string id, ReviewTariffRuleInput input, PricingService service, HttpContext context)
            => Results.Ok(await service.ReviewRule(Actor.From(context), id, input)));
        pricing.MapPost("/rules/{id}/retire", async (string id, RetireTariffRuleInput input, PricingService service, HttpContext context)
            => Results.Ok(await service.RetireRule(Actor.From(context), id, input)));
        secure.MapPost("/pricing/simulate", async (SimulatePricingInput input, CatalogService catalog, HttpContext context)
            => Results.Ok(await catalog.SimulatePricing(Actor.From(context), input)));
        secure.MapGet("/orders/{id}/pricing-history", async (string id, PricingService service, HttpContext context)
            => Results.Ok(await service.History(Actor.From(context), id,
                CatalogService.Page(context.Request.Query["page"], 1), CatalogService.Page(context.Request.Query["pageSize"], 25))));
    }
}
