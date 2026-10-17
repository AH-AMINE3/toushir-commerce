namespace Toushir.Wholesale;

public static class CrmEndpoints
{
    public static void MapCrm(this RouteGroupBuilder secure)
    {
        secure.MapGet("/crm", async (CrmService service, HttpContext context) => Results.Ok(await service.Overview(Actor.From(context))));
        secure.MapGet("/crm/shops/{id}", async (string id, CrmService service, HttpContext context) => Results.Ok(await service.Dossier(Actor.From(context), id)));
        secure.MapPost("/crm/shops/{id}/portfolio", async (string id, AssignPortfolioInput input, CrmService service, HttpContext context) => Results.Ok(await service.Assign(Actor.From(context), id, input)));
        secure.MapPost("/crm/shops/{id}/tasks", async (string id, CreateCrmTaskInput input, CrmService service, HttpContext context) => Results.Ok(await service.CreateTask(Actor.From(context), id, input)));
        secure.MapPost("/crm/shops/{id}/contacts", async (string id, LogCrmContactInput input, CrmService service, HttpContext context) => Results.Ok(await service.LogContact(Actor.From(context), id, input)));
        secure.MapPost("/crm/tasks/{id}/reschedule", async (string id, RescheduleCrmTaskInput input, CrmService service, HttpContext context) => Results.Ok(await service.Reschedule(Actor.From(context), id, input)));
        secure.MapPost("/crm/tasks/{id}/finish", async (string id, FinishCrmTaskInput input, CrmService service, HttpContext context) => Results.Ok(await service.Finish(Actor.From(context), id, input)));
        secure.MapPost("/crm/tasks/{id}/cancel", async (string id, CancelCrmTaskInput input, CrmService service, HttpContext context) => Results.Ok(await service.Cancel(Actor.From(context), id, input)));
        secure.MapPost("/crm/assistance/{id}/use", async (string id, UseCrmAssistanceInput input, CrmService service, HttpContext context) => Results.Ok(await service.UseAssistance(Actor.From(context), id, input)));
        secure.MapGet("/shop/assistance", async (CrmService service, HttpContext context) => Results.Ok(await service.MerchantAssistance(Actor.From(context))));
        secure.MapPost("/shop/assistance", async (GrantCrmAssistanceInput input, CrmService service, HttpContext context) => Results.Ok(await service.Grant(Actor.From(context), input)));
        secure.MapPost("/shop/assistance/{id}/revoke", async (string id, RevokeCrmAssistanceInput input, CrmService service, HttpContext context) => Results.Ok(await service.RevokeAssistance(Actor.From(context), id, input)));
    }
}
