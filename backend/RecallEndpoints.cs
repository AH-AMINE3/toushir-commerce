namespace Toushir.Wholesale;
public record CreateRecallInput(string LotId, int Version, string Reason, string SafetyInstruction, string IdempotencyKey);
public record RecoverRecallInput(int Version, string PositionId, int Quantity, string? QuarantineLotId, string ProofReference, string IdempotencyKey);
public record CancelRecallPreparationInput(int Version, string OrderId, int OrderVersion, string Reason, string IdempotencyKey);
public record RecallExceptionInput(int Version, string PositionId, int Quantity, string Reason, string ProofReference, string IdempotencyKey);
public record CloseRecallInput(int Version, string Note, string? UnknownScopeReason, string IdempotencyKey);
public record AcknowledgeRecallInput(int Version, string IdempotencyKey);
public static class RecallEndpoints
{
    public static void MapRecalls(this RouteGroupBuilder secure)
    {
        secure.MapGet("/admin/recalls", async (RecallService s, HttpContext c) => Results.Ok(await s.List(Actor.From(c))));
        secure.MapPost("/admin/recalls", async (CreateRecallInput x, RecallService s, HttpContext c) => Results.Ok(await s.Create(Actor.From(c), x)));
        secure.MapGet("/admin/recalls/{id}", async (string id, RecallService s, HttpContext c) => Results.Ok(await s.Detail(Actor.From(c), id)));
        secure.MapPost("/admin/recalls/{id}/recover", async (string id, RecoverRecallInput x, RecallService s, HttpContext c) => Results.Ok(await s.Recover(Actor.From(c), id, x)));
        secure.MapPost("/admin/recalls/{id}/cancel-preparation", async (string id, CancelRecallPreparationInput x, RecallService s, HttpContext c) => Results.Ok(await s.CancelPreparation(Actor.From(c), id, x)));
        secure.MapPost("/admin/recalls/{id}/exceptions", async (string id, RecallExceptionInput x, RecallService s, HttpContext c) => Results.Ok(await s.Exception(Actor.From(c), id, x)));
        secure.MapPost("/admin/recalls/{id}/close", async (string id, CloseRecallInput x, RecallService s, HttpContext c) => Results.Ok(await s.Close(Actor.From(c), id, x)));
        secure.MapGet("/recalls/inbox", async (RecallService s, HttpContext c) => Results.Ok(await s.Inbox(Actor.From(c))));
        secure.MapPost("/recalls/notices/{id}/ack", async (string id, AcknowledgeRecallInput x, RecallService s, HttpContext c) => Results.Ok(await s.Acknowledge(Actor.From(c), id, x)));
    }
}
