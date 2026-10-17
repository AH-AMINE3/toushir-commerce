namespace Toushir.Wholesale;

public static class OperationsEndpoints
{
    public static void MapCommerceOperations(this WebApplication app, RouteGroupBuilder secure)
    {
        var operations = secure.MapGroup("/operations");
        operations.MapGet("/dashboard", async (OperationsService service, HttpContext context) => Results.Ok(await service.Dashboard(Actor.From(context))));
        operations.MapGet("/team", async (OperationsService service, HttpContext context) => Results.Ok(await service.Team(Actor.From(context))));
        operations.MapPost("/team", async (CreateTeamInput input, OperationsService service, HttpContext context) => Results.Ok(await service.CreateTeam(Actor.From(context), input)));
        operations.MapGet("/orders/{id}", async (string id, OperationsService service, HttpContext context) => Results.Ok(await service.StaffOrder(Actor.From(context),id)));
        operations.MapGet("/orders", async (OperationsService service, HttpContext context) => Results.Ok(await service.List(Actor.From(context))));
        operations.MapPatch("/orders/{id}/preparation", async (string id, PrepareInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Prepare(Actor.From(context), id, input)));
        operations.MapPost("/orders/{id}/assign", async (string id, AssignDriverInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Assign(Actor.From(context), id, input)));
        operations.MapPost("/orders/{id}/returns/inspect", async (string id, InspectReturnInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Inspect(Actor.From(context), id, input)));
        operations.MapPost("/orders/{id}/payments", async (string id, PaymentInput input, OperationsService service, HttpContext context) => Results.Ok(await service.FinanceCollect(Actor.From(context), id, input)));
        operations.MapPost("/orders/{id}/refunds", async (string id, RefundInput input, OperationsService service, HttpContext context) => Results.Ok(await service.FinanceRefund(Actor.From(context), id, input)));
        operations.MapPost("/payments/{id}/reconcile", async (string id, OperationsService service, HttpContext context) => Results.Ok(await service.Reconcile(Actor.From(context), id)));
        secure.MapGet("/orders/{id}/tracking", async (string id, OperationsService service, HttpContext context) => Results.Ok(await service.Tracking(Actor.From(context), id)));
        secure.MapPost("/orders/{id}/receipt-challenge", async (string id, ReceiptProofInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Challenge(Actor.From(context), id, input)));
        var courier = secure.MapGroup("/courier");
        courier.MapGet("/manifest", async (OperationsService service, HttpContext context) => Results.Ok(await service.List(Actor.From(context), true)));
        courier.MapPost("/orders/{id}/pickup", async (string id, VersionInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Pickup(Actor.From(context), id, input)));
        courier.MapPost("/orders/{id}/status", async (string id, CourierStatusInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Status(Actor.From(context), id, input)));
        courier.MapPost("/location", async (DriverLocationInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Location(Actor.From(context), input)));
        courier.MapPost("/orders/{id}/complete", async (string id, CompleteDeliveryInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Complete(Actor.From(context), id, input)));
        courier.MapPost("/orders/{id}/collect", async (string id, CourierCollectionInput input, OperationsService service, HttpContext context) => Results.Ok(await service.Collect(Actor.From(context), id, input)));
    }
}
