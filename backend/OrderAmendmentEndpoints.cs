namespace Toushir.Wholesale;

public static class OrderAmendmentEndpoints
{
    public static void MapOrderAmendments(this RouteGroupBuilder secure)
    {
        secure.MapGet("/orders/{id}/amendments", async (string id, OrderAmendmentService service, HttpContext c) => Results.Ok(await service.List(Actor.From(c), id)));
        secure.MapPost("/admin/orders/{id}/amendments", async (string id, ProposeOrderAmendmentInput input, OrderAmendmentService service, HttpContext c) => Results.Ok(await service.Propose(Actor.From(c), id, input)));
        secure.MapPost("/orders/{id}/amendments/{proposalId}/accept", async (string id, string proposalId, DecideOrderAmendmentInput input, OrderAmendmentService service, HttpContext c) => Results.Ok(await service.Decide(Actor.From(c), id, proposalId, input, "ACCEPTED")));
        secure.MapPost("/orders/{id}/amendments/{proposalId}/reject", async (string id, string proposalId, DecideOrderAmendmentInput input, OrderAmendmentService service, HttpContext c) => Results.Ok(await service.Decide(Actor.From(c), id, proposalId, input, "REJECTED")));
        secure.MapPost("/admin/orders/{id}/amendments/{proposalId}/withdraw", async (string id, string proposalId, DecideOrderAmendmentInput input, OrderAmendmentService service, HttpContext c) => Results.Ok(await service.Decide(Actor.From(c), id, proposalId, input, "WITHDRAWN")));
    }
}
