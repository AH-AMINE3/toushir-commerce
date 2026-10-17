using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public class ClaimsService(WholesaleDb db, OrderService orders, PromotionsService promotions, WarehouseService warehouse)
{
    public static object View(OrderClaim c) => new { c.Id, c.OrderId, c.ProductId, c.ProductName, c.Quantity, c.Reason, c.Status, c.ResolutionNote, c.CreatedAt, c.ResolvedAt, c.ReceivedQuantity, c.InspectedQuantity, c.CreditedAmount };
    public static async Task EnsureSchema(WholesaleDb db)
    {
        // This compatibility upgrade is for existing SQLite previews. PostgreSQL's
        // initial schema is created by EF with its native numeric/timestamp types.
        if (!db.Database.IsSqlite())
            return;
        await db.Database.ExecuteSqlRawAsync("""
   CREATE TABLE IF NOT EXISTS Claims (Id TEXT NOT NULL PRIMARY KEY, WorkspaceId TEXT NOT NULL, OrderId TEXT NOT NULL, AccountId TEXT NOT NULL, ProductId TEXT NOT NULL, ProductName TEXT NOT NULL, Quantity INTEGER NOT NULL, Reason TEXT NOT NULL, Status TEXT NOT NULL, ResolutionNote TEXT NOT NULL, ReceivedQuantity INTEGER NOT NULL, InspectedQuantity INTEGER NOT NULL, CreditedAmount TEXT NOT NULL, CreatedAt TEXT NOT NULL, ResolvedAt TEXT NULL);
   CREATE INDEX IF NOT EXISTS IX_Claims_WorkspaceId_OrderId ON Claims (WorkspaceId, OrderId);
   CREATE TABLE IF NOT EXISTS Credits (Id TEXT NOT NULL PRIMARY KEY, WorkspaceId TEXT NOT NULL, OrderId TEXT NOT NULL, ClaimId TEXT NOT NULL, IdempotencyKey TEXT NOT NULL, RequestHash TEXT NOT NULL, Amount TEXT NOT NULL, Reason TEXT NOT NULL, Actor TEXT NOT NULL, At TEXT NOT NULL);
   CREATE UNIQUE INDEX IF NOT EXISTS IX_Credits_WorkspaceId_ClaimId_IdempotencyKey ON Credits (WorkspaceId, ClaimId, IdempotencyKey);
   """);
    }
    public async Task<OrderClaim> Find(Actor a, string id)
    {
        a.Admin();
        var c = await db.Claims.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == a.WorkspaceId);
        Check.That(c != null, "Réclamation introuvable.", "NOT_FOUND", 404);
        return c!;
    }
    public async Task<object> Create(Actor a, string orderId, CreateClaimInput input)
    {
        Check.That(a.Role == "MERCHANT", "Utilisez le compte commerçant pour signaler un problème.", "MERCHANT_REQUIRED", 403);
        var o = await orders.Find(a, orderId);
        Check.That(o.Status is "DELIVERED" or "PARTIALLY_DELIVERED", "Une réclamation concerne une commande reçue.", "INVALID_TRANSITION", 409);
        var l = o.Items.FirstOrDefault(x => x.ProductId == input.ProductId);
        Check.That(l != null, "Produit absent de cette commande.", "NOT_FOUND", 404);
        var existing = await db.Claims.Where(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == orderId && x.ProductId == input.ProductId && x.Status != "REJECTED").ToListAsync();
        Check.That(input.Quantity > 0 && input.Quantity <= l!.AcceptedQuantity - existing.Sum(x => x.Quantity), "La quantité dépasse les unités acceptées encore réclamables.", "CLAIM_QUANTITY", 409);
        var reason = Check.Text(input.Reason, 1500, "Motif de réclamation");
        var claim = new OrderClaim { WorkspaceId = a.WorkspaceId, OrderId = orderId, AccountId = a.Id, ProductId = l.ProductId, ProductName = l.Name, Quantity = input.Quantity, Reason = reason };
        db.Claims.Add(claim);
        orders.Event(a, o, "CLAIM_OPENED", $"{input.Quantity} × {l.Name} : {reason}");
        await db.SaveChangesAsync();
        return View(claim);
    }
    public async Task<object> List(Actor a, IQueryCollection query)
    {
        a.Admin();
        var status = query["status"].ToString();
        var claims = (await db.Claims.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && (status == "" || x.Status == status)).ToListAsync()).OrderByDescending(x => x.CreatedAt).ToList();
        var list = await db.Orders.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToDictionaryAsync(x => x.Id);
        var accounts = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToDictionaryAsync(x => x.Id);
        return AdminService.Paged(claims.Select(c => (object)new { c.Id, c.OrderId, orderNumber = list.GetValueOrDefault(c.OrderId)?.Number, shopName = accounts.GetValueOrDefault(c.AccountId)?.ShopName, c.ProductId, c.ProductName, c.Quantity, c.Reason, c.Status, c.ResolutionNote, c.CreatedAt, c.ResolvedAt, c.ReceivedQuantity, c.InspectedQuantity, c.CreditedAmount }).ToList(), query);
    }
    public async Task<object> Resolve(Actor a, string id, ResolveClaimInput input)
    {
        var c = await Find(a, id);
        var status = input.Status.ToUpperInvariant();
        Check.That(status is "ACCEPTED" or "REJECTED", "Décision de réclamation invalide.");
        var note = Check.Text(input.Note, 1500, "Motif de décision");
        if (c.Status == status && c.ResolutionNote == note)
            return View(c);
        Check.That(c.Status == "OPEN", "Cette réclamation a déjà fait l'objet d'une décision.", "CLAIM_ALREADY_RESOLVED", 409);
        c.Status = status;
        c.ResolutionNote = note;
        c.ResolvedAt = DateTimeOffset.UtcNow;
        var o = await orders.Find(a, c.OrderId);
        orders.Event(a, o, "CLAIM_" + status, $"{c.Id}: {note}. Aucun mouvement physique ni financier automatique.");
        await db.SaveChangesAsync();
        return View(c);
    }
    public async Task<object> Receive(Actor a, string id, ReceiveClaimInput input)
    {
        var c = await Find(a, id);
        Check.That(c.Status == "ACCEPTED", "La réclamation doit être acceptée avant réception.", "INVALID_TRANSITION", 409);
        Check.That(input.Quantity > 0 && input.Quantity <= c.Quantity - c.ReceivedQuantity, "La quantité reçue dépasse le retour autorisé.", "CLAIM_QUANTITY", 409);
        var note = Check.Text(input.Note, 1000, "Constat de réception");
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Offers" SET "Quarantined" = "Quarantined" + {input.Quantity} WHERE "WorkspaceId" = {a.WorkspaceId} AND "ProductId" = {c.ProductId}""");
        Check.That(changed == 1, "La référence de stock est introuvable.", "INVENTORY_CONFLICT", 409);
        c.ReceivedQuantity += input.Quantity;
        var o = await orders.Find(a, c.OrderId);
        orders.Ledger(a, c.ProductId, "CLAIM_RETURN_RECEIVED", quarantine: input.Quantity, orderId: o.Id, reason: $"Réclamation {c.Id}: {note}");
        orders.Event(a, o, "CLAIM_RETURN_RECEIVED", $"{input.Quantity} unités physiquement reçues en quarantaine. {note}");
        await db.SaveChangesAsync();
        await promotions.ReconcileReturns(a, o);
        await db.SaveChangesAsync();
        return View(c);
    }
    public async Task<object> Inspect(Actor a, string id, InspectClaimInput input)
    {
        var c = await Find(a, id);
        Check.That(c.Status == "ACCEPTED" && input.Quantity > 0 && input.Quantity <= c.ReceivedQuantity - c.InspectedQuantity, "Seules les unités physiquement reçues et non contrôlées peuvent être inspectées.", "CLAIM_QUANTITY", 409);
        var disposition = input.Disposition.ToUpperInvariant();
        Check.That(disposition is "RESTOCK" or "WRITE_OFF", "Choisissez une remise en stock ou une perte.");
        var reason = Check.Text(input.Reason, 1000, "Motif de contrôle");
        await warehouse.InspectReturn(a, c.OrderId, c.ProductId, "CLAIM", c.Id, input.Quantity, disposition);
        var delta = disposition == "RESTOCK" ? input.Quantity : 0;
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Offers" SET "Quarantined" = "Quarantined" - {input.Quantity}, "OnHand" = COALESCE("OnHand",0) + {delta} WHERE "WorkspaceId" = {a.WorkspaceId} AND "ProductId" = {c.ProductId} AND "Quarantined" >= {input.Quantity}""");
        Check.That(changed == 1, "Stock en quarantaine insuffisant.", "INVENTORY_CONFLICT", 409);
        c.InspectedQuantity += input.Quantity;
        var o = await orders.Find(a, c.OrderId);
        orders.Ledger(a, c.ProductId, "CLAIM_" + disposition, delta: delta, quarantine: -input.Quantity, orderId: o.Id, reason: $"Réclamation {c.Id}: {reason}");
        orders.Event(a, o, "CLAIM_RETURN_INSPECTED", $"{input.Quantity} unités : {disposition}. {reason}");
        await db.SaveChangesAsync();
        return View(c);
    }
    public async Task<object> Credit(Actor a, string id, CreditClaimInput input)
    {
        var c = await Find(a, id);
        var o = await orders.Find(a, c.OrderId);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d'idempotence");
        var reason = Check.Text(input.Reason, 1000, "Motif de l'avoir");
        Check.Money(input.Amount, "Montant de l'avoir");
        var hash = Tokens.Hash(JsonSerializer.Serialize(new
        {
            input.Amount,
            reason
        }));
        var old = await db.Credits.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.ClaimId == id && x.IdempotencyKey == key);
        if (old != null)
        {
            Check.That(old.Amount == input.Amount && old.Reason == reason, "Cette clé correspond à un avoir différent.", "IDEMPOTENCY_CONFLICT", 409);
            return await orders.View(a, o);
        }
        Check.That(c.Status == "ACCEPTED" && c.ReceivedQuantity > 0, "Un avoir nécessite une réclamation acceptée et un retour physiquement reçu.", "RETURN_REQUIRED", 409);
        var line = o.Items.First(x => x.ProductId == c.ProductId);
        var limit = c.ReceivedQuantity * (line.UnitPrice ?? 0) - c.CreditedAmount;
        Check.That(input.Amount > 0 && input.Amount <= limit && o.Subtotal >= input.Amount, "Le montant dépasse la valeur des unités reçues encore créditables.", "CREDIT_EXCEEDS_RETURN", 409);
        db.Credits.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            OrderId = o.Id,
            ClaimId = id,
            IdempotencyKey = key,
            RequestHash = hash,
            Amount = input.Amount,
            Reason = reason,
            Actor = a.Name
        });
        c.CreditedAmount += input.Amount;
        o.Subtotal -= input.Amount;
        o.Total = o.Subtotal + o.DeliveryFee;
        OrderService.UpdatePayment(o);
        orders.Event(a, o, "COMMERCIAL_CREDIT_ISSUED", $"Avoir explicite {input.Amount:0.00} DZD pour la réclamation {id}. {reason}. Aucun remboursement automatique.");
        await db.SaveChangesAsync();
        await promotions.ReconcileReturns(a, o);
        await db.SaveChangesAsync();
        return await orders.View(a, o);
    }
}
