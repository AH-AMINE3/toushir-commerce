namespace Toushir.Wholesale;

public static class SupplyDocumentEndpoints
{
    public static void MapSupplierDocumentEndpoints(this RouteGroupBuilder secure)
    {
        var purchases = secure.MapGroup("/admin/purchases/{id}");
        purchases.MapGet("/dossier", async (string id, SupplyDocumentService service, HttpContext context)
            => Results.Ok(await service.Dossier(Actor.From(context), id)));
        purchases.MapPost("/documents", async (string id, UploadSupplierDocumentInput input, SupplyDocumentService service, HttpContext context)
            => Results.Ok(await service.Upload(Actor.From(context), id, input)));
        purchases.MapGet("/documents/{documentId}/content", async (string id, string documentId, SupplyDocumentService service, HttpContext context) =>
        {
            var file = await service.Content(Actor.From(context), id, documentId);
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(file.Content, "application/pdf", file.FileName);
        });
        purchases.MapPost("/documents/{documentId}/void", async (string id, string documentId, VoidSupplierDocumentInput input, SupplyDocumentService service, HttpContext context)
            => Results.Ok(await service.Void(Actor.From(context), id, documentId, input)));
        purchases.MapPost("/confirmations", async (string id, RecordSupplierConfirmationInput input, SupplyDocumentService service, HttpContext context)
            => Results.Ok(await service.RecordConfirmation(Actor.From(context), id, input)));
        purchases.MapPost("/confirmations/{confirmationId}/review", async (string id, string confirmationId, ReviewSupplierConfirmationInput input, SupplyDocumentService service, HttpContext context)
            => Results.Ok(await service.ReviewConfirmation(Actor.From(context), id, confirmationId, input)));
        purchases.MapPost("/cancel", async (string id, CloseSupplyOrderInput input, SupplyDocumentService service, HttpContext context)
            => Results.Ok(await service.Close(Actor.From(context), id, input, false)));
        purchases.MapPost("/close-remainder", async (string id, CloseSupplyOrderInput input, SupplyDocumentService service, HttpContext context)
            => Results.Ok(await service.Close(Actor.From(context), id, input, true)));
    }
}
