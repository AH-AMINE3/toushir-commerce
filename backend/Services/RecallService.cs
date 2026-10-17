using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace Toushir.Wholesale;

public static class RecallGuard
{
    public static string Identity(string product, string number, DateTimeOffset? expiry) => Tokens.Hash(JsonSerializer.Serialize(new { product, number, expiry = expiry?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) }));
    public static async Task<HashSet<string>> HeldIdentities(WholesaleDb db, string workspace, string product) => (await db.RecallCases.AsNoTracking().Where(x => x.WorkspaceId == workspace && x.ProductId == product && x.ContainmentActive).Select(x => x.IdentityKey).ToListAsync()).ToHashSet();
    public static async Task AssertLot(WholesaleDb db, string workspace, WarehouseLot lot)
    {
        var identity = Identity(lot.ProductId, lot.LotNumber, lot.ExpiresAt);
        Check.That(!await db.RecallCases.AnyAsync(x => x.WorkspaceId == workspace && x.IdentityKey == identity && x.ContainmentActive), "Ce lot fait l'objet d'un rappel. Isolez-le; aucune préparation, sortie ou remise en vente n'est autorisée.", "LOT_RECALLED", 409);
    }
    public static async Task<bool> HasHeldReservations(WholesaleDb db, PurchaseOrder order)
    {
        var ids = await db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == order.WorkspaceId && x.OrderId == order.Id && x.Status == "RESERVED" && x.LotId != "").Select(x => x.LotId).ToListAsync();
        var lots = await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == order.WorkspaceId && ids.Contains(x.Id)).ToListAsync();
        var identities = lots.Select(x => Identity(x.ProductId, x.LotNumber, x.ExpiresAt)).ToList();
        return await db.RecallCases.AnyAsync(x => x.WorkspaceId == order.WorkspaceId && x.ContainmentActive && identities.Contains(x.IdentityKey));
    }
    public static async Task AssertDistribution(WholesaleDb db, PurchaseOrder order, List<string>? acceptedProducts = null)
    {
        var query = db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == order.WorkspaceId && x.OrderId == order.Id && (x.Status == "RESERVED" || x.Status == "DISPATCHED") && x.LotId != "");
        if (acceptedProducts != null) query = query.Where(x => acceptedProducts.Contains(x.ProductId));
        var ids = await query.Select(x => x.LotId).ToListAsync();
        foreach (var lot in await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == order.WorkspaceId && ids.Contains(x.Id)).ToListAsync())
            await AssertLot(db, order.WorkspaceId, lot);
    }
    public static async Task AssertOrder(WholesaleDb db, PurchaseOrder order)
    {
        var ids = await db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == order.WorkspaceId && x.OrderId == order.Id && x.Status == "RESERVED" && x.LotId != "").Select(x => x.LotId).ToListAsync();
        foreach (var lot in await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == order.WorkspaceId && ids.Contains(x.Id)).ToListAsync())
            await AssertLot(db, order.WorkspaceId, lot);
    }
}

/// <summary>Containment, real traced exposure and attested quarantine reconciliation. Notifications never assert physical recovery.</summary>
public sealed class RecallService(WholesaleDb db, RealtimeChanges changes, OrderService orders)
{
    private static string Hash(object value) => Tokens.Hash(JsonSerializer.Serialize(value));
    private static int Remaining(RecallPosition p) => p.Quantity - p.RecoveredQuantity - p.ExceptionQuantity;
    private async Task<RecallCase> Case(Actor a, string id)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE");
        var recall = await db.RecallCases.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.Id == id);
        Check.That(recall != null, "Dossier de rappel introuvable.", "NOT_FOUND", 404);
        return recall;
    }
    private async Task<RecallOperation?> Replay(Actor a, string key, string hash)
    {
        Check.Text(key, 100, "Clé d'idempotence");
        var old = await db.RecallOperations.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.ActorId == a.Id && x.IdempotencyKey == key);
        if (old != null) Check.That(old.RequestHash == hash, "Clé déjà utilisée pour une opération différente.", "IDEMPOTENCY_CONFLICT", 409);
        return old;
    }
    private void Operation(Actor a, string key, string hash, string recall) => db.RecallOperations.Add(new() { WorkspaceId = a.WorkspaceId, ActorId = a.Id, IdempotencyKey = key, RequestHash = hash, RecallId = recall });
    private void Event(Actor a, RecallCase r, string kind, string detail, string position = "", int quantity = 0, string lot = "", string proof = "", string destination = "")
    {
        db.RecallEvents.Add(new() { WorkspaceId = a.WorkspaceId, RecallId = r.Id, ActorId = a.Id, Kind = kind, Details = detail, PositionId = position, Quantity = quantity, WarehouseLotId = lot, ProofReference = proof, Destination = destination });
        db.Audit.Add(new() { WorkspaceId = a.WorkspaceId, ActorId = a.Id, Actor = a.Name, Action = kind, EntityId = r.Id, Details = detail });
    }
    private async Task<List<WarehouseLot>> IdentityLots(RecallCase r) => (await db.WarehouseLots.Where(x => x.WorkspaceId == r.WorkspaceId && x.ProductId == r.ProductId && x.LotNumber == r.LotNumber).ToListAsync()).Where(x => RecallGuard.Identity(x.ProductId, x.LotNumber, x.ExpiresAt) == r.IdentityKey).ToList();
    public async Task<object> List(Actor a)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE");
        var recalls = await db.RecallCases.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        var positions = await db.RecallPositions.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        var products = await db.Products.AsNoTracking().Where(x => recalls.Select(r => r.ProductId).Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return new { items = recalls.OrderByDescending(x => x.CreatedAt).Select(r => new { recall = r, productName = products.GetValueOrDefault(r.ProductId)?.Name, exposed = positions.Where(p => p.RecallId == r.Id && p.RequiresRecovery).Sum(p => p.Quantity), recovered = positions.Where(p => p.RecallId == r.Id && p.RequiresRecovery).Sum(p => p.RecoveredQuantity), residual = positions.Where(p => p.RecallId == r.Id && p.RequiresRecovery).Sum(Remaining) }) };
    }
    public async Task<object> Detail(Actor a, string id)
    {
        var r = await Case(a, id);
        var positions = await db.RecallPositions.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.RecallId == id).ToListAsync();
        var orderIds = positions.Select(x => x.OrderId).Where(x => x.Length > 0).Distinct().ToList();
        var orders = await db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.WorkspaceId == a.WorkspaceId && orderIds.Contains(x.Id)).ToListAsync();
        var allocations = await db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && orderIds.Contains(x.OrderId) && x.ProductId == r.ProductId).ToListAsync();
        var assignments = await db.DeliveryAssignments.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && orderIds.Contains(x.OrderId)).ToListAsync();
        var shops = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && positions.Select(p => p.ShopId).Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return new
        {
            recall = r,
            product = await db.Products.AsNoTracking().Where(x => x.Id == r.ProductId).Select(x => new { x.Id, x.Name, x.Barcode }).FirstOrDefaultAsync(),
            positions = positions.Select(p => new
            {
                p.Id, p.Kind, p.LotId, p.AllocationId, p.OrderId, p.ShopId, p.DriverId, p.Quantity, p.RecoveredQuantity, p.ExceptionQuantity, p.RequiresRecovery, p.Version, p.ExceptionReason, p.ExceptionReference, p.ExceptionBy, p.ExceptionAt,
                remaining = Remaining(p), shopName = shops.GetValueOrDefault(p.ShopId)?.ShopName,
                orderNumber = orders.FirstOrDefault(o => o.Id == p.OrderId)?.Number,
                orderStatus = orders.FirstOrDefault(o => o.Id == p.OrderId)?.Status ?? p.SnapshotOrderStatus,
                orderVersion = orders.FirstOrDefault(o => o.Id == p.OrderId)?.Version,
                driverCustody = assignments.FirstOrDefault(x => x.OrderId == p.OrderId),
                // These are order/product aggregates. Mixed allocations cannot identify which lot was refused.
                acceptedProductQuantity = orders.FirstOrDefault(o => o.Id == p.OrderId)?.Items.Where(l => l.ProductId == r.ProductId).Sum(l => l.AcceptedQuantity),
                refusedProductQuantity = orders.FirstOrDefault(o => o.Id == p.OrderId)?.Items.Where(l => l.ProductId == r.ProductId).Sum(l => l.RefusedQuantity),
                mixedLotDisposition = allocations.Where(x => x.OrderId == p.OrderId && x.Status == "DISPATCHED").Select(x => x.LotId).Distinct().Count() > 1
            }),
            lots = (await IdentityLots(r)).Select(x => new { x.Id, x.SourceKey, x.SourceRef, x.OnHand, x.Reserved, x.Quarantined, x.Version }),
            notices = await db.RecallNotices.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.RecallId == id).ToListAsync(),
            events = (await db.RecallEvents.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.RecallId == id).ToListAsync()).OrderBy(x => x.At),
            unknownScope = new { historicalOrderCount = r.UnknownUntrackedOrderCount, historicalQuantity = r.UnknownUntrackedQuantity, note = "Historique agrégé sans lot identifié : aucune appartenance à ce lot n'est affirmée. Les quantités livrées/refusées par produit ne localisent pas un lot au sein d'une livraison mixte." },
            recoveryPolicy = "Dépôt : isolation physique du stock non réservé. Hors dépôt : réception réelle du retour puis identification du lot avant rapprochement. Aucun avoir ni remboursement automatique. La clôture maintient le blocage du lot."
        };
    }
    public async Task<object> Create(Actor a, CreateRecallInput input)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE");
        var reason = Check.Text(input.Reason, 2000, "Motif du rappel");
        var instruction = Check.Text(input.SafetyInstruction, 2000, "Consigne de sécurité");
        var hash = Hash(new { kind = "CREATE_RECALL", input.LotId, input.Version, reason, instruction });
        var old = await Replay(a, input.IdempotencyKey, hash);
        if (old != null) return await Detail(a, old.RecallId);
        var lot = await db.WarehouseLots.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.Id == input.LotId);
        Check.That(lot != null, "Lot compté introuvable. Le stock agrégé ne peut pas être présenté comme un lot tracé.", "NOT_FOUND", 404);
        Check.That(await db.LotProductControls.AnyAsync(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == lot.ProductId), "Activez le contrôle physique par lots avant de déclarer ce rappel.", "NOT_LOT_MANAGED", 409);
        Check.That(lot.Version == input.Version, "Le lot a changé. Actualisez le comptage.", "VERSION_CONFLICT", 409);
        var identity = RecallGuard.Identity(lot.ProductId, lot.LotNumber, lot.ExpiresAt);
        Check.That(!await db.RecallCases.AnyAsync(x => x.WorkspaceId == a.WorkspaceId && x.IdentityKey == identity), "Ce lot possède déjà un dossier de rappel.", "RECALL_EXISTS", 409);
        var r = new RecallCase { WorkspaceId = a.WorkspaceId, ProductId = lot.ProductId, LotId = lot.Id, IdentityKey = identity, LotNumber = lot.LotNumber, ExpiresAt = lot.ExpiresAt, Reason = reason, SafetyInstruction = instruction, CreatedBy = a.Id };
        db.RecallCases.Add(r);
        var lots = await IdentityLots(r); var ids = lots.Select(x => x.Id).ToList();
        foreach (var l in lots)
        {
            if (l.OnHand > 0) db.RecallPositions.Add(new() { WorkspaceId = a.WorkspaceId, RecallId = r.Id, PositionKey = "DEPOT:" + l.Id, Kind = "DEPOT", LotId = l.Id, Quantity = l.OnHand });
            if (l.Quarantined > 0) db.RecallPositions.Add(new() { WorkspaceId = a.WorkspaceId, RecallId = r.Id, PositionKey = "ISOLATED:" + l.Id, Kind = "ISOLATED", LotId = l.Id, Quantity = l.Quarantined, RecoveredQuantity = l.Quarantined });
        }
        var allocations = await db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && ids.Contains(x.LotId) && (x.Status == "RESERVED" || x.Status == "DISPATCHED")).ToListAsync();
        var orderIds = allocations.Select(x => x.OrderId).Distinct().ToList();
        var orders = await db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.WorkspaceId == a.WorkspaceId && orderIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var assignments = await db.DeliveryAssignments.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && orderIds.Contains(x.OrderId)).ToDictionaryAsync(x => x.OrderId);
        var claims = await db.Claims.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && orderIds.Contains(x.OrderId) && x.ProductId == r.ProductId).ToListAsync();
        var gifts = await db.PromotionSnapshots.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && orderIds.Contains(x.OrderId) && x.GiftProductId == r.ProductId).ToListAsync();
        foreach (var group in allocations.GroupBy(x => x.OrderId))
        {
            if (!orders.TryGetValue(group.Key, out var order)) continue;
            var refs = order.Items.Where(x => x.ProductId == r.ProductId).Select(x => "REFUSAL:" + x.Id).Concat(claims.Where(x => x.OrderId == order.Id).Select(x => "CLAIM:" + x.Id)).Concat(gifts.Where(x => x.OrderId == order.Id).Select(x => "PROMOTION:" + x.Id)).ToHashSet();
            // Identified historical returns are already in depot/quarantine; do not count them again outside.
            var returned = lots.Where(x => x.SourceKey.StartsWith("RETURN:") && refs.Contains(x.SourceRef)).Sum(x => x.InitialQuantity);
            foreach (var allocation in group.OrderBy(x => x.Id))
            {
                var net = allocation.Quantity;
                if (allocation.Status == "DISPATCHED") { var subtract = Math.Min(returned, net); net -= subtract; returned -= subtract; }
                if (net == 0) continue;
                db.RecallPositions.Add(new() { WorkspaceId = a.WorkspaceId, RecallId = r.Id, PositionKey = "ALLOCATION:" + allocation.Id, Kind = allocation.Status == "RESERVED" ? "PREPARATION" : "OUTBOUND", LotId = allocation.LotId, AllocationId = allocation.Id, OrderId = order.Id, ShopId = order.AccountId, DriverId = assignments.GetValueOrDefault(order.Id)?.DriverId ?? "", SnapshotOrderStatus = order.Status, Quantity = net, RequiresRecovery = allocation.Status == "DISPATCHED" });
            }
        }
        var allProductAllocations = await db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == r.ProductId && x.Status == "DISPATCHED").ToListAsync();
        var historical = await db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.WorkspaceId == a.WorkspaceId && (x.Status == "OUT_FOR_DELIVERY" || x.Status == "DELIVERED" || x.Status == "PARTIALLY_DELIVERED" || x.Status == "DELIVERY_REFUSED") && x.Items.Any(l => l.ProductId == r.ProductId)).ToListAsync();
        var unknown = historical.Select(o => new { o.Id, Quantity = o.Items.Where(l => l.ProductId == r.ProductId).Sum(l => l.Quantity) - allProductAllocations.Where(x => x.OrderId == o.Id && x.LotId != "" && x.OrderLineId != "").Sum(x => x.Quantity) }).Where(x => x.Quantity > 0).ToList();
        r.UnknownUntrackedOrderCount = unknown.Count; r.UnknownUntrackedQuantity = unknown.Sum(x => x.Quantity);
        Event(a, r, "RECALL_CONTAINMENT_ACTIVE", reason + " · " + instruction);
        // Instructions are available in-app only after the persistent hold is established in this transaction.
        foreach (var shop in orders.Values.Select(x => x.AccountId).Distinct())
        {
            db.RecallNotices.Add(new() { WorkspaceId = a.WorkspaceId, RecallId = r.Id, RecipientId = shop, RecipientRole = "MERCHANT" });
            db.EngagementNotices.Add(new() { WorkspaceId = a.WorkspaceId, MerchantId = shop, Kind = "LOT_RECALL", AggregateId = r.Id });
        }
        foreach (var driver in assignments.Values.Select(x => x.DriverId).Where(x => x.Length > 0).Distinct()) db.RecallNotices.Add(new() { WorkspaceId = a.WorkspaceId, RecallId = r.Id, RecipientId = driver, RecipientRole = "DRIVER" });
        foreach (var order in orders.Values) changes.Order(a.WorkspaceId, order.Id, order.AccountId);
        Operation(a, input.IdempotencyKey, hash, r.Id);
        await db.SaveChangesAsync();
        return await Detail(a, r.Id);
    }
    public async Task<object> Recover(Actor a, string id, RecoverRecallInput input)
    {
        var r = await Case(a, id);
        var proof = Check.Text(input.ProofReference, 500, "Référence de réception physique");
        var hash = Hash(new { kind = "RECALL_RECOVER", id, input.Version, input.PositionId, input.Quantity, input.QuarantineLotId, proof });
        if (await Replay(a, input.IdempotencyKey, hash) != null) return await Detail(a, id);
        Check.That(r.Status == "ACTIVE" && r.Version == input.Version, "Le dossier a changé ou est clôturé.", "VERSION_CONFLICT", 409);
        var p = await db.RecallPositions.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.RecallId == id && x.Id == input.PositionId);
        Check.That(p != null && p.RequiresRecovery && p.Kind is "DEPOT" or "OUTBOUND", "Cette position ne peut pas recevoir une récupération.", "RECALL_POSITION", 409);
        Check.That(input.Quantity > 0 && input.Quantity <= Remaining(p), "La quantité dépasse l'exposition tracée encore ouverte.", "RECALL_QUANTITY", 409);
        WarehouseLot lot;
        if (p.Kind == "DEPOT")
        {
            lot = await db.WarehouseLots.FirstAsync(x => x.WorkspaceId == a.WorkspaceId && x.Id == p.LotId);
            Check.That(input.QuarantineLotId == null || input.QuarantineLotId == lot.Id, "L'isolation doit concerner le lot exact du dépôt.", "LOT_MISMATCH", 409);
            Check.That(lot.OnHand - lot.Reserved >= input.Quantity, "La quantité est insuffisante ou encore réservée. Libérez la réservation par le flux de commande avant l'isolation physique.", "LOT_STOCK_CONFLICT", 409);
            var q = input.Quantity;
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"OnHand\"=COALESCE(\"OnHand\",0)-{q}, \"Quarantined\"=\"Quarantined\"+{q} WHERE \"WorkspaceId\"={a.WorkspaceId} AND \"ProductId\"={r.ProductId} AND COALESCE(\"OnHand\",0)-\"Reserved\">={q} AND \"Quarantined\"+{q}<=1000000000");
            Check.That(changed == 1, "Le stock physique a changé.", "STOCK_CONFLICT", 409);
            lot.OnHand -= q; lot.Quarantined += q; lot.Version++;
            db.Inventory.Add(new() { WorkspaceId = a.WorkspaceId, ProductId = r.ProductId, Kind = "RECALL_DEPOT_QUARANTINE", Delta = -q, QuarantineDelta = q, Actor = a.Name, Reason = r.LotNumber + " · " + proof });
        }
        else
        {
            Check.That(!string.IsNullOrWhiteSpace(input.QuarantineLotId), "Réceptionnez le retour et identifiez le lot en quarantaine avant le rapprochement.", "PHYSICAL_RETURN_REQUIRED", 409);
            var found = await db.WarehouseLots.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.Id == input.QuarantineLotId);
            Check.That(found != null && found.SourceKey.StartsWith("RETURN:") && RecallGuard.Identity(found.ProductId, found.LotNumber, found.ExpiresAt) == r.IdentityKey, "Le retour identifié ne correspond pas au lot rappelé.", "LOT_MISMATCH", 409);
            lot = found;
            Check.That(!await db.RecallPositions.AnyAsync(x => x.RecallId == id && x.Kind == "ISOLATED" && x.LotId == lot.Id), "Cette quarantaine était déjà comptée au dépôt lors de l'ouverture.", "RECALL_ALREADY_ACCOUNTED", 409);
            var order = await db.Orders.AsNoTracking().Include(x => x.Items).FirstAsync(x => x.WorkspaceId == a.WorkspaceId && x.Id == p.OrderId);
            var validSource = order.Items.Any(x => x.ProductId == r.ProductId && lot.SourceRef == "REFUSAL:" + x.Id)
                || await db.Claims.AnyAsync(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == p.OrderId && x.ProductId == r.ProductId && lot.SourceRef == "CLAIM:" + x.Id)
                || await db.PromotionSnapshots.AnyAsync(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == p.OrderId && x.GiftProductId == r.ProductId && lot.SourceRef == "PROMOTION:" + x.Id);
            Check.That(validSource, "Cette réception physique appartient à une autre commande.", "RETURN_SOURCE_MISMATCH", 409);
            var linked = await db.RecallEvents.Where(x => x.WorkspaceId == a.WorkspaceId && x.WarehouseLotId == lot.Id && x.Kind == "RECALL_RETURN_LINKED").SumAsync(x => x.Quantity);
            Check.That(input.Quantity <= lot.Quarantined - linked, "La quarantaine reçue est insuffisante ou déjà rapprochée.", "RECALL_QUANTITY", 409);
            // No inventory delta: the ordinary return receipt already added these real units to quarantine.
        }
        p.RecoveredQuantity += input.Quantity; p.Version++; r.Version++;
        Event(a, r, p.Kind == "DEPOT" ? "RECALL_DEPOT_ISOLATED" : "RECALL_RETURN_LINKED", "Réception physique attestée par " + a.Name, p.Id, input.Quantity, lot.Id, proof, "QUARANTINE");
        Operation(a, input.IdempotencyKey, hash, id);
        await db.SaveChangesAsync();
        return await Detail(a, id);
    }
    public async Task<object> CancelPreparation(Actor a, string id, CancelRecallPreparationInput input)
    {
        a.Admin(); var r = await Case(a, id);
        var reason = Check.Text(input.Reason, 2000, "Motif d'annulation de la préparation");
        Check.That(reason.Length >= 20, "Précisez le motif de l'annulation et le devenir commercial de la commande (20 caractères minimum).");
        var hash = Hash(new { kind = "RECALL_CANCEL_PREPARATION", id, input.Version, input.OrderId, input.OrderVersion, reason });
        if (await Replay(a, input.IdempotencyKey, hash) != null) return await Detail(a, id);
        Check.That(r.Status == "ACTIVE" && r.Version == input.Version, "Le dossier a changé ou est clôturé.", "VERSION_CONFLICT", 409);
        var position = await db.RecallPositions.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.RecallId == id && x.OrderId == input.OrderId && x.Kind == "PREPARATION");
        Check.That(position != null && await db.LotAllocations.AnyAsync(x => x.WorkspaceId == a.WorkspaceId && x.Id == position.AllocationId && x.Status == "RESERVED"), "Cette commande n'a plus de préparation réservée concernée par ce rappel.", "RECALL_RESERVATION_REQUIRED", 409);
        await orders.CancelRecalledPreparation(a, input.OrderId, input.OrderVersion, reason);
        r.Version++;
        Event(a, r, "RECALL_PREPARATION_CANCELLED", reason + " · Commande " + input.OrderId + " : contrat annulé, réservations libérées; isolement physique encore à confirmer.", position.Id);
        Operation(a, input.IdempotencyKey, hash, id); await db.SaveChangesAsync(); return await Detail(a, id);
    }
    public async Task<object> Exception(Actor a, string id, RecallExceptionInput input)
    {
        a.Admin(); var r = await Case(a, id);
        var reason = Check.Text(input.Reason, 2000, "Décision résiduelle motivée");
        var proof = Check.Text(input.ProofReference, 500, "Référence de décision");
        Check.That(reason.Length >= 20, "Expliquez précisément le devenir des unités non récupérées (20 caractères minimum).");
        var hash = Hash(new { kind = "RECALL_EXCEPTION", id, input.Version, input.PositionId, input.Quantity, reason, proof });
        if (await Replay(a, input.IdempotencyKey, hash) != null) return await Detail(a, id);
        Check.That(r.Status == "ACTIVE" && r.Version == input.Version, "Le dossier a changé ou est clôturé.", "VERSION_CONFLICT", 409);
        var p = await db.RecallPositions.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.RecallId == id && x.Id == input.PositionId);
        Check.That(p != null && p.Kind == "OUTBOUND" && input.Quantity > 0 && input.Quantity <= Remaining(p), "Une exception ne peut concerner que l'exposition extérieure encore ouverte. Le stock connu du dépôt doit être physiquement isolé.", "RECALL_QUANTITY", 409);
        p.ExceptionQuantity += input.Quantity; p.ExceptionReason = reason; p.ExceptionReference = proof; p.ExceptionBy = a.Id; p.ExceptionAt = DateTimeOffset.UtcNow; p.Version++; r.Version++;
        Event(a, r, "RECALL_RESIDUAL_EXCEPTION", reason, p.Id, input.Quantity, proof: proof, destination: "UNRECOVERED_EXCEPTION");
        Operation(a, input.IdempotencyKey, hash, id); await db.SaveChangesAsync(); return await Detail(a, id);
    }
    public async Task<object> Close(Actor a, string id, CloseRecallInput input)
    {
        a.Admin(); var r = await Case(a, id);
        var note = Check.Text(input.Note, 2000, "Décision de clôture");
        var unknown = Check.Text(input.UnknownScopeReason, 2000, "Décision sur l'historique non tracé", false);
        var hash = Hash(new { kind = "CLOSE_RECALL", id, input.Version, note, unknown });
        if (await Replay(a, input.IdempotencyKey, hash) != null) return await Detail(a, id);
        Check.That(r.Status == "ACTIVE" && r.Version == input.Version, "Le dossier a changé ou est déjà clôturé.", "VERSION_CONFLICT", 409);
        var positions = await db.RecallPositions.Where(x => x.WorkspaceId == a.WorkspaceId && x.RecallId == id && x.RequiresRecovery).ToListAsync();
        Check.That(positions.All(x => Remaining(x) == 0), "Isolez les quantités connues et décidez explicitement chaque exception résiduelle avant la clôture.", "RECALL_UNRESOLVED", 409);
        Check.That((await IdentityLots(r)).All(x => x.OnHand == 0 && x.Reserved == 0), "Des unités du lot restent au dépôt ou en préparation. Le blocage seul ne prouve pas leur isolation physique.", "RECALL_UNRESOLVED", 409);
        Check.That(r.UnknownUntrackedQuantity == 0 || unknown.Length >= 20, "L'historique sans lot doit recevoir une décision motivée explicite; il ne peut pas être déclaré récupéré.", "UNTRACKED_SCOPE_UNRESOLVED", 409);
        r.Status = "CLOSED"; r.ClosedAt = DateTimeOffset.UtcNow; r.ClosedBy = a.Id; r.ClosureNote = note; r.UnknownScopeReason = unknown; r.Version++;
        Event(a, r, "RECALL_CLOSED_HOLD_RETAINED", note + (unknown.Length > 0 ? " · Historique non tracé : " + unknown : ""));
        Operation(a, input.IdempotencyKey, hash, id); await db.SaveChangesAsync(); return await Detail(a, id);
    }
    public async Task<object> Inbox(Actor a)
    {
        a.RequireRoles("MERCHANT", "DRIVER");
        var recipient = a.Role == "MERCHANT" ? a.ResourceOwnerId : a.Id;
        var notices = await db.RecallNotices.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.RecipientRole == a.Role && x.RecipientId == recipient).ToListAsync();
        var ids = notices.Select(x => x.RecallId).ToList();
        var recalls = await db.RecallCases.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var positions = await db.RecallPositions.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && ids.Contains(x.RecallId) && (a.Role == "MERCHANT" ? x.ShopId == recipient : x.DriverId == recipient)).ToListAsync();
        return new { items = notices.OrderByDescending(x => x.CreatedAt).Select(n => new { notice = n, recall = new { recalls[n.RecallId].Id, recalls[n.RecallId].ProductId, recalls[n.RecallId].LotNumber, recalls[n.RecallId].ExpiresAt, recalls[n.RecallId].SafetyInstruction, recalls[n.RecallId].Status, recalls[n.RecallId].ContainmentActive }, positions = positions.Where(p => p.RecallId == n.RecallId).Select(p => new { p.Id, p.Kind, p.OrderId, p.Quantity, p.RecoveredQuantity, p.ExceptionQuantity }), receiptMeaning = "Cet accusé confirme la réception de la consigne. Il ne confirme ni son application ni une récupération physique." }) };
    }
    public async Task<object> Acknowledge(Actor a, string id, AcknowledgeRecallInput input)
    {
        a.RequireRoles("MERCHANT", "DRIVER"); if (a.Role == "MERCHANT") a.ShopWrite();
        var recipient = a.Role == "MERCHANT" ? a.ResourceOwnerId : a.Id;
        var notice = await db.RecallNotices.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.Id == id && x.RecipientRole == a.Role && x.RecipientId == recipient);
        Check.That(notice != null, "Consigne introuvable.", "NOT_FOUND", 404);
        var hash = Hash(new { kind = "ACK_RECALL", id, input.Version });
        if (await Replay(a, input.IdempotencyKey, hash) != null) return await Inbox(a);
        Check.That(notice.Version == input.Version && notice.ReceivedAt == null, "La réception a déjà été enregistrée ou a évolué.", "VERSION_CONFLICT", 409);
        notice.ReceivedAt = DateTimeOffset.UtcNow; notice.ReceivedBy = a.Id; notice.Version++;
        var r = await db.RecallCases.FirstAsync(x => x.WorkspaceId == a.WorkspaceId && x.Id == notice.RecallId);
        Event(a, r, "RECALL_INSTRUCTION_RECEIVED", "Accusé de réception de la consigne uniquement; aucune récupération physique déclarée.");
        Operation(a, input.IdempotencyKey, hash, r.Id); await db.SaveChangesAsync(); return await Inbox(a);
    }
}
