using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed partial class EngagementService
{
    public async Task<object> Suppliers(Actor actor)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "FINANCE");
        return new { items = await db.Suppliers.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).OrderBy(x => x.Name).ToListAsync() };
    }
    public async Task<object> CreateSupplier(Actor actor, SupplierInput input)
    {
        actor.Admin(); var supplier = new Supplier { WorkspaceId = actor.WorkspaceId, Name = Check.Text(input.Name, 180, "Fournisseur"), Contact = Check.Text(input.Contact, 180, "Contact"), Phone = Check.Text(input.Phone, 40, "Téléphone"), Note = Check.Text(input.Note, 2000, "Note", false) };
        db.Suppliers.Add(supplier); orders.Audit(actor, "SUPPLIER_CREATED", supplier.Id); await db.SaveChangesAsync(); return supplier;
    }
    private async Task<SupplyOrder> Supply(Actor actor, string id)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "FINANCE"); var order = await db.SupplyOrders.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(order != null, "Achat fournisseur introuvable.", "NOT_FOUND", 404); return order;
    }
    private async Task<object> SupplyView(SupplyOrder order)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstAsync(x => x.Id == order.SupplierId && x.WorkspaceId == order.WorkspaceId);
        var lines = await db.SupplyOrderLines.AsNoTracking().Where(x => x.SupplyOrderId == order.Id && x.WorkspaceId == order.WorkspaceId).ToListAsync();
        var receipts = await db.SupplyReceipts.AsNoTracking().Where(x => x.SupplyOrderId == order.Id && x.WorkspaceId == order.WorkspaceId).ToListAsync();
        var receiptIds = receipts.Select(x => x.Id).ToList(); var receiptLines = await db.SupplyReceiptLines.AsNoTracking().Where(x => x.WorkspaceId == order.WorkspaceId && receiptIds.Contains(x.ReceiptId)).ToListAsync();
        return new { order, supplier, items = lines.Select(x => new { x.Id, x.ProductId, x.ProductName, x.OrderedQuantity, x.ReceivedQuantity, x.QuarantineQuantity, outstandingQuantity = x.OrderedQuantity - x.ReceivedQuantity, x.UnitCostMinor, totalMinor = checked(x.OrderedQuantity * x.UnitCostMinor) }), receipts, receiptLines, totalMinor = lines.Sum(x => checked(x.OrderedQuantity * x.UnitCostMinor)), receivedMinor = lines.Sum(x => checked(x.ReceivedQuantity * x.UnitCostMinor)), currency = "DZD", monetaryUnit = "CENTIME" };
    }
    public async Task<object> Purchases(Actor actor)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "FINANCE"); var list = (await db.SupplyOrders.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).ToListAsync()).OrderByDescending(x => x.CreatedAt).Take(250).ToList();
        var ids = list.Select(x => x.Id).ToList(); var lines = await db.SupplyOrderLines.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && ids.Contains(x.SupplyOrderId)).ToListAsync();
        var suppliers = await db.Suppliers.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).ToDictionaryAsync(x => x.Id, x => x.Name);
        return new { items = list.Select(x => new { x.Id, x.Number, x.SupplierId, supplierName = suppliers.GetValueOrDefault(x.SupplierId), x.Status, x.Version, x.CreatedAt, x.ExpectedAt, orderedQuantity = lines.Where(l => l.SupplyOrderId == x.Id).Sum(l => l.OrderedQuantity), receivedQuantity = lines.Where(l => l.SupplyOrderId == x.Id).Sum(l => l.ReceivedQuantity), totalMinor = lines.Where(l => l.SupplyOrderId == x.Id).Sum(l => checked(l.UnitCostMinor * l.OrderedQuantity)), x.InvoiceMatchStatus }), monetaryUnit = "CENTIME", currency = "DZD" };
    }
    public async Task<object> Purchase(Actor actor, string id) => await SupplyView(await Supply(actor, id));
    public async Task<object> CreatePurchase(Actor actor, CreateSupplyInput input)
    {
        actor.Admin(); var key = Key(input.IdempotencyKey); Check.That(input.Items is { Count: > 0 and <= 100 } && input.Items.Select(x => x.ProductId).Distinct().Count() == input.Items.Count, "Saisissez entre 1 et 100 lignes distinctes.");
        foreach (var line in input.Items) Check.That(line.Quantity is >= 1 and <= 1000000 && line.UnitCostMinor is >= 0 and <= 10000000000, "Quantité ou coût invalide.");
        var hash = Hash(new { input.SupplierId, input.ExpectedAt, items = input.Items.OrderBy(x => x.ProductId).ToList(), note = input.Note?.Trim() ?? "" });
        var old = await db.SupplyOrders.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.IdempotencyKey == key); if (old != null) { Check.That(old.RequestHash == hash, "Clé déjà utilisée pour un autre achat.", "IDEMPOTENCY_CONFLICT", 409); return await SupplyView(old); }
        Check.That(await db.Suppliers.AnyAsync(x => x.Id == input.SupplierId && x.WorkspaceId == actor.WorkspaceId && x.Active), "Fournisseur actif introuvable.");
        var ids = input.Items.Select(x => x.ProductId).ToList(); var products = await db.Products.AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsCatalogActive).ToDictionaryAsync(x => x.Id);
        Check.That(products.Count == input.Items.Count, "Produit introuvable.");
        var order = new SupplyOrder { WorkspaceId = actor.WorkspaceId, SupplierId = input.SupplierId, ExpectedAt = input.ExpectedAt, Note = Check.Text(input.Note, 2000, "Note", false), Number = "ACH-" + DateTimeOffset.UtcNow.ToString("yyMMdd") + "-" + IdGen.New()[..8].ToUpperInvariant(), CreatedBy = actor.Id, IdempotencyKey = key, RequestHash = hash };
        db.SupplyOrders.Add(order);
        foreach (var line in input.Items) db.SupplyOrderLines.Add(new() { WorkspaceId = actor.WorkspaceId, SupplyOrderId = order.Id, ProductId = line.ProductId, ProductName = products[line.ProductId].Name, OrderedQuantity = line.Quantity, UnitCostMinor = line.UnitCostMinor });
        orders.Audit(actor, "SUPPLY_ORDER_CREATED", order.Id); Notice(actor, "", "SUPPLY_ORDER_CREATED", order.Id); await db.SaveChangesAsync(); return await SupplyView(order);
    }
    public async Task<object> ApprovePurchase(Actor actor, string id, VersionInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE"); var order = await Supply(actor, id); Version(order.Version, input.Version);
        Check.That(order.Status == "DRAFT", "Cet achat est déjà approuvé.", "INVALID_TRANSITION", 409); Check.That(order.CreatedBy != actor.Id, "Une autre personne habilitée doit approuver l'achat.", "INDEPENDENT_APPROVAL_REQUIRED", 409);
        order.Status = "APPROVED"; order.ApprovedBy = actor.Id; order.Version++; orders.Audit(actor, "SUPPLY_ORDER_APPROVED", id); await db.SaveChangesAsync(); return await SupplyView(order);
    }
    public async Task<object> ReceivePurchase(Actor actor, string id, ReceiveSupplyInput input)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE"); var order = await Supply(actor, id); var key = Key(input.IdempotencyKey);
        Check.That(input.Lines is { Count: > 0 and <= 100 } && input.Lines.Select(x => x.ProductId).Distinct().Count() == input.Lines.Count, "Chaque produit doit apparaître une seule fois.");
        var hash = Hash(new { lines = input.Lines.OrderBy(x => x.ProductId).ToList(), note = input.Note?.Trim() ?? "" });
        var old = await db.SupplyReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.SupplyOrderId == id && x.IdempotencyKey == key); if (old != null) { Check.That(old.RequestHash == hash, "Clé déjà utilisée pour une autre réception.", "IDEMPOTENCY_CONFLICT", 409); return await SupplyView(order); }
        Version(order.Version, input.Version); Check.That(order.Status is "APPROVED" or "PARTIAL", "L'achat doit être approuvé et ouvert.", "INVALID_TRANSITION", 409);
        var lines = await db.SupplyOrderLines.Where(x => x.SupplyOrderId == id && x.WorkspaceId == actor.WorkspaceId).ToListAsync();
        var receipt = new SupplyReceipt { WorkspaceId = actor.WorkspaceId, SupplyOrderId = id, ActorId = actor.Id, IdempotencyKey = key, RequestHash = hash, Note = Check.Text(input.Note, 2000, "Note", false) }; db.SupplyReceipts.Add(receipt);
        foreach (var item in input.Lines)
        {
            var line = lines.FirstOrDefault(x => x.ProductId == item.ProductId); Check.That(line != null && item.ReceivedQuantity > 0 && item.ReceivedQuantity <= line.OrderedQuantity - line.ReceivedQuantity && item.QuarantineQuantity >= 0 && item.QuarantineQuantity <= item.ReceivedQuantity, "La réception dépasse le reliquat ou la quarantaine est invalide.");
            var lot = Check.Text(item.LotNumber, 100, "Lot");
            Check.That(item.ExpiresAt == null || item.ExpiresAt > DateTimeOffset.UtcNow || item.QuarantineQuantity == item.ReceivedQuantity, "Un lot expiré doit être entièrement isolé.");
            var offer = await db.Offers.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.ProductId == item.ProductId);
            if (offer == null) { offer = new() { WorkspaceId = actor.WorkspaceId, ProductId = item.ProductId, OnHand = 0 }; db.Offers.Add(offer); await db.SaveChangesAsync(); }
            db.Entry(offer).State = EntityState.Detached;
            var conforming = item.ReceivedQuantity - item.QuarantineQuantity;
            var stockChanged = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"OnHand\"=COALESCE(\"OnHand\",0)+{conforming}, \"Quarantined\"=\"Quarantined\"+{item.QuarantineQuantity}, \"UpdatedAt\"={DateTimeOffset.UtcNow} WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"ProductId\"={item.ProductId} AND COALESCE(\"OnHand\",0)<={1000000000 - conforming} AND \"Quarantined\"<={1000000000 - item.QuarantineQuantity}");
            Check.That(stockChanged == 1, "Le mouvement de stock dépasse les bornes ou la référence a changé.", "STOCK_CONFLICT", 409);
            line.ReceivedQuantity += item.ReceivedQuantity; line.QuarantineQuantity += item.QuarantineQuantity;
            db.SupplyReceiptLines.Add(new() { WorkspaceId = actor.WorkspaceId, ReceiptId = receipt.Id, ProductId = item.ProductId, ReceivedQuantity = item.ReceivedQuantity, QuarantineQuantity = item.QuarantineQuantity, LotNumber = lot, ExpiresAt = item.ExpiresAt });
            orders.Ledger(actor, item.ProductId, "SUPPLY_RECEIVED", delta: conforming, quarantine: item.QuarantineQuantity, reason: $"{order.Number}; lot {lot}; réception {receipt.Id}");
        }
        order.Status = lines.All(x => x.ReceivedQuantity == x.OrderedQuantity) ? "RECEIVED" : "PARTIAL"; order.Version++; orders.Audit(actor, "SUPPLY_RECEIVED", id, receipt.Id); Notice(actor, "", "SUPPLY_RECEIVED", id);
        await db.SaveChangesAsync(); return await SupplyView(order);
    }
    public async Task<object> InspectSupply(Actor actor, string orderId, InspectSupplyInput input)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE"); var order = await Supply(actor, orderId); var line = await db.SupplyReceiptLines.FirstOrDefaultAsync(x => x.Id == input.ReceiptLineId && x.WorkspaceId == actor.WorkspaceId);
        Check.That(line != null && await db.SupplyReceipts.AnyAsync(x => x.Id == line.ReceiptId && x.SupplyOrderId == orderId && x.WorkspaceId == actor.WorkspaceId), "Lot reçu introuvable.", "NOT_FOUND", 404);
        var key = Key(input.IdempotencyKey); var disposition = input.Disposition.ToUpperInvariant(); var reason = Check.Text(input.Reason, 1000, "Motif"); var hash = Hash(new { input.Quantity, disposition, reason });
        var old = await db.SupplyInspections.AsNoTracking().FirstOrDefaultAsync(x => x.ReceiptLineId == line.Id && x.IdempotencyKey == key); if (old != null) { Check.That(old.RequestHash == hash, "Clé déjà utilisée.", "IDEMPOTENCY_CONFLICT", 409); return old; }
        Version(line.Version, input.Version); Check.That(input.Quantity > 0 && input.Quantity <= line.QuarantineQuantity - line.InspectedQuantity, "Quantité isolée insuffisante.");
        Check.That(disposition is "RELEASE" or "SUPPLIER_RETURN" or "DESTROY", "Disposition inconnue.");
        Check.That(disposition != "RELEASE" || line.ExpiresAt == null || line.ExpiresAt > DateTimeOffset.UtcNow, "Un produit expiré ne peut pas être remis en vente.");
        var delta = disposition == "RELEASE" ? input.Quantity : 0;
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"Quarantined\"=\"Quarantined\"-{input.Quantity}, \"OnHand\"=COALESCE(\"OnHand\",0)+{delta} WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"ProductId\"={line.ProductId} AND \"Quarantined\">={input.Quantity}");
        Check.That(changed == 1, "Stock isolé incohérent.", "STOCK_CONFLICT", 409); line.InspectedQuantity += input.Quantity; line.Version++;
        var inspection = new SupplyInspection { WorkspaceId = actor.WorkspaceId, ReceiptLineId = line.Id, IdempotencyKey = key, RequestHash = hash, Quantity = input.Quantity, Disposition = disposition, Reason = reason, ActorId = actor.Id }; db.SupplyInspections.Add(inspection);
        orders.Ledger(actor, line.ProductId, "SUPPLY_" + disposition, delta: delta, quarantine: -input.Quantity, reason: reason); orders.Audit(actor, "SUPPLY_INSPECTED", order.Id, inspection.Id);
        await db.SaveChangesAsync(); return inspection;
    }
    public async Task<object> ReviewInvoice(Actor actor, string id, InvoiceReviewInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE"); var order = await Supply(actor, id); Version(order.Version, input.Version);
        Check.That(order.Status is "PARTIAL" or "RECEIVED", "Une réception est requise pour le rapprochement.", "INVALID_TRANSITION", 409); Check.That(input.InvoiceMinor is >= 0 and <= 10000000000000000, "Montant invalide.");
        var lines = await db.SupplyOrderLines.AsNoTracking().Where(x => x.SupplyOrderId == id).ToListAsync(); var received = lines.Sum(x => checked(x.UnitCostMinor * x.ReceivedQuantity));
        order.InvoiceReference = Check.Text(input.InvoiceReference, 180, "Facture fournisseur"); order.InvoiceMinor = input.InvoiceMinor; order.InvoiceMatchStatus = input.InvoiceMinor == received ? "MATCHED" : "DISCREPANCY"; order.Version++;
        orders.Audit(actor, "SUPPLIER_INVOICE_REVIEWED", id, $"{input.InvoiceMinor}/{received} centimes; {order.InvoiceMatchStatus}"); await db.SaveChangesAsync(); return new { order, expectedReceivedMinor = received, differenceMinor = input.InvoiceMinor - received, paymentRecorded = false };
    }
}
