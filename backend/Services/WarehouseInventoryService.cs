using Microsoft.EntityFrameworkCore;
namespace Toushir.Wholesale;

public sealed partial class WarehouseService
{
    private async Task<WarehouseLot> Lot(Actor a, string id)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE");
        var lot = await db.WarehouseLots.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == a.WorkspaceId);
        Check.That(lot != null, "Lot introuvable.", "NOT_FOUND", 404);
        return lot;
    }
    private async Task LockProduct(string workspace, string product)
    {
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"Quarantined\"=\"Quarantined\" WHERE \"WorkspaceId\"={workspace} AND \"ProductId\"={product}");
        Check.That(changed == 1, "Stock introuvable.", "STOCK_CONFLICT", 409);
    }
    public async Task<object> CountLot(Actor a, string id, CountLotInput input)
    {
        var lot = await Lot(a, id);
        var reason = Check.Text(input.Reason, 1000, "Motif");
        var proof = Check.Text(input.ProofReference, 300, "Preuve de comptage");
        var hash = Hash(new
        {
            kind = "COUNT_LOT",
            id,
            input.Quantity,
            reason,
            proof
        });
        if (await Replay(a, input.IdempotencyKey, hash) != null)
            return await Lots(a, lot.ProductId);
        await RecallGuard.AssertLot(db, a.WorkspaceId, lot);
        Check.That(lot.Version == input.Version, "Lot modifié depuis le comptage.", "VERSION_CONFLICT", 409);
        Check.That(input.Quantity >= lot.Reserved && input.Quantity <= 1000000000, "Le comptage ne peut pas consommer une réserve existante.");
        Check.That(lot.ExpiresAt == null || lot.ExpiresAt > DateTimeOffset.UtcNow || input.Quantity == 0, "Isolez le lot expiré; ne le déclarez pas vendable.");
        var delta = input.Quantity - lot.OnHand;
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"OnHand\"=COALESCE(\"OnHand\",0)+{delta} WHERE \"WorkspaceId\"={a.WorkspaceId} AND \"ProductId\"={lot.ProductId} AND COALESCE(\"OnHand\",0)+{delta}>=\"Reserved\" AND COALESCE(\"OnHand\",0)+{delta}<=1000000000");
        Check.That(changed == 1, "La réserve a changé pendant le comptage.", "STOCK_CONFLICT", 409);
        lot.OnHand = input.Quantity;
        lot.Version++;
        db.Inventory.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            ProductId = lot.ProductId,
            Kind = "LOT_COUNTED",
            Delta = delta,
            Actor = a.Name,
            Reason = reason + " · " + proof + " · " + lot.LotNumber
        });
        Operation(a, input.IdempotencyKey, hash, "LOT_COUNTED", lot.Id, input.Quantity);
        Audit(a, "LOT_COUNTED", id, reason + " · " + proof);
        await db.SaveChangesAsync();
        return await Lots(a, lot.ProductId);
    }
    public async Task<object> Quality(Actor a, string id, LotQualityInput input)
    {
        var lot = await Lot(a, id);
        var reason = Check.Text(input.Reason, 1000, "Motif");
        var disposition = Check.Text(input.Disposition, 30, "Disposition").ToUpperInvariant();
        var hash = Hash(new
        {
            kind = "LOT_QUALITY",
            id,
            input.Quantity,
            disposition,
            reason
        });
        if (await Replay(a, input.IdempotencyKey, hash) != null)
            return await Lots(a, lot.ProductId);
        await RecallGuard.AssertLot(db, a.WorkspaceId, lot);
        Check.That(lot.Version == input.Version, "La version du lot a changé.", "VERSION_CONFLICT", 409);
        Check.That(input.Quantity > 0 && disposition is "QUARANTINE" or "RELEASE" or "DESTROY", "Décision qualité invalide.");
        var q = input.Quantity;
        int physical, quarantine;
        Check.That(disposition == "QUARANTINE" || lot.SourceKey.StartsWith("COUNT:") || lot.SourceKey.StartsWith("QUALITY:"), "Utilisez le contrôle de la réception fournisseur ou du retour d'origine pour cette quarantaine.", "ORIGIN_INSPECTION_REQUIRED", 409);
        if (disposition == "QUARANTINE")
        {
            Check.That(lot.OnHand - lot.Reserved >= q, "Le lot est réservé ou insuffisant.", "LOT_STOCK_CONFLICT", 409);
            physical = -q;
            quarantine = q;
        }
        else
        {
            Check.That(lot.Quarantined >= q, "Quantité isolée insuffisante.");
            Check.That(disposition != "RELEASE" || lot.ExpiresAt == null || lot.ExpiresAt > DateTimeOffset.UtcNow, "Un lot expiré ne peut pas être libéré.");
            physical = disposition == "RELEASE" ? q : 0;
            quarantine = -q;
        }
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"OnHand\"=COALESCE(\"OnHand\",0)+{physical}, \"Quarantined\"=\"Quarantined\"+{quarantine} WHERE \"WorkspaceId\"={a.WorkspaceId} AND \"ProductId\"={lot.ProductId} AND COALESCE(\"OnHand\",0)+{physical}>=\"Reserved\" AND \"Quarantined\"+{quarantine}>=0 AND COALESCE(\"OnHand\",0)+{physical}<=1000000000 AND \"Quarantined\"+{quarantine}<=1000000000");
        Check.That(changed == 1, "Stock modifié pendant le contrôle.", "STOCK_CONFLICT", 409);
        lot.OnHand += physical;
        if (disposition == "QUARANTINE")
            db.WarehouseLots.Add(new()
            {
                WorkspaceId = a.WorkspaceId,
                ProductId = lot.ProductId,
                LotNumber = lot.LotNumber,
                ExpiresAt = lot.ExpiresAt,
                Quarantined = q,
                InitialQuantity = q,
                SourceKey = "QUALITY:" + IdGen.New(),
                SourceRef = lot.Id,
                ReceivedAt = lot.ReceivedAt
            });
        else
            lot.Quarantined += quarantine;
        lot.Version++;
        db.Inventory.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            ProductId = lot.ProductId,
            Kind = "LOT_" + disposition,
            Delta = physical,
            QuarantineDelta = quarantine,
            Actor = a.Name,
            Reason = lot.LotNumber + " · " + reason
        });
        Operation(a, input.IdempotencyKey, hash, "LOT_" + disposition, lot.Id, q);
        Audit(a, "LOT_" + disposition, id, reason);
        await db.SaveChangesAsync();
        return await Lots(a, lot.ProductId);
    }
    public async Task RecordSupplyLot(Actor a, SupplyReceiptLine line)
    {
        if (!await IsManaged(a.WorkspaceId, line.ProductId))
            return;
        if (line.ReceivedQuantity > line.QuarantineQuantity)
            await RecallGuard.AssertLot(db, a.WorkspaceId, new WarehouseLot { ProductId = line.ProductId, LotNumber = line.LotNumber, ExpiresAt = line.ExpiresAt });
        var source = "SUPPLY:" + line.Id;
        if (await db.WarehouseLots.AnyAsync(x => x.WorkspaceId == a.WorkspaceId && x.SourceKey == source))
            return;
        db.WarehouseLots.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            ProductId = line.ProductId,
            LotNumber = line.LotNumber,
            ExpiresAt = line.ExpiresAt?.ToUniversalTime(),
            OnHand = line.ReceivedQuantity - line.QuarantineQuantity,
            Quarantined = line.QuarantineQuantity,
            InitialQuantity = line.ReceivedQuantity,
            SourceKey = source,
            SourceRef = source
        });
    }
    public async Task InspectSupplyLot(Actor a, SupplyReceiptLine line, int quantity, string disposition)
    {
        if (!await IsManaged(a.WorkspaceId, line.ProductId))
            return;
        var lot = await db.WarehouseLots.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.SourceKey == "SUPPLY:" + line.Id);
        Check.That(lot != null && lot.Quarantined >= quantity, "Le lot fournisseur doit être rapproché avant contrôle.", "LOT_REVIEW_REQUIRED", 409);
        if (disposition == "RELEASE") await RecallGuard.AssertLot(db, a.WorkspaceId, lot);
        lot.Quarantined -= quantity;
        if (disposition == "RELEASE")
            lot.OnHand += quantity;
        lot.Version++;
    }
    private async Task<(string Product, string Order, int Remaining)> ReturnSource(Actor a, string type, string sourceId)
    {
        if (type == "CLAIM")
        {
            var claim = await db.Claims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sourceId && x.WorkspaceId == a.WorkspaceId);
            Check.That(claim != null && claim.Status == "ACCEPTED", "Retour accepté introuvable.", "NOT_FOUND", 404);
            return (claim.ProductId, claim.OrderId, claim.ReceivedQuantity - claim.InspectedQuantity);
        }
        if (type == "REFUSAL")
        {
            var line = await db.Set<OrderLine>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == sourceId);
            Check.That(line != null && await db.Orders.AnyAsync(x => x.Id == line.PurchaseOrderId && x.WorkspaceId == a.WorkspaceId), "Ligne refusée introuvable.", "NOT_FOUND", 404);
            var routed = await db.DeliveryAssignments.AsNoTracking().AnyAsync(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == line.PurchaseOrderId && x.RouteId != "");
            Check.That(!routed || await db.OrderEvents.AsNoTracking().AnyAsync(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == line.PurchaseOrderId && x.Action == "ROUTE_GOODS_RETURNED"), "Le dépôt doit confirmer le retour physique de cette tournée avant identification ou inspection du lot.", "RETURN_CUSTODY_OPEN", 409);
            return (line.ProductId, line.PurchaseOrderId, line.RefusedQuantity - line.InspectedQuantity);
        }
        Check.That(type == "PROMOTION", "Origine de retour inconnue.");
        var gift = await db.PromotionSnapshots.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sourceId && x.WorkspaceId == a.WorkspaceId);
        Check.That(gift != null && gift.GiftStockState == "RETURNED_QUARANTINE", "Le cadeau doit être physiquement reçu en quarantaine.", "INVALID_TRANSITION", 409);
        return (gift.GiftProductId, gift.OrderId, gift.GiftRefusedQuantity);
    }
    public async Task<object> IdentifyReturn(Actor a, IdentifyReturnLotInput input)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE");
        var type = Check.Text(input.SourceType, 20, "Origine").ToUpperInvariant();
        var number = Check.Text(input.LotNumber, 100, "Lot");
        var proof = Check.Text(input.ProofReference, 300, "Preuve physique");
        var hash = Hash(new
        {
            kind = "IDENTIFY_RETURN",
            type,
            input.SourceId,
            input.Quantity,
            number,
            input.ExpiresAt,
            proof
        });
        var old = await Replay(a, input.IdempotencyKey, hash);
        if (old != null)
        {
            var existing = await db.WarehouseLots.AsNoTracking().FirstAsync(x => x.Id == old.ResultId);
            return await Lots(a, existing.ProductId);
        }
        var source = await ReturnSource(a, type, input.SourceId);
        Check.That(await IsManaged(a.WorkspaceId, source.Product), "Ce produit n'est pas encore géré par lots.", "NOT_LOT_MANAGED", 409);
        await LockProduct(a.WorkspaceId, source.Product);
        source = await ReturnSource(a, type, input.SourceId);
        var reference = type + ":" + input.SourceId;
        var existingLots = await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.SourceRef == reference).ToListAsync();
        Check.That(input.Quantity > 0 && input.Quantity <= source.Remaining - existingLots.Sum(x => x.Quarantined), "La quantité dépasse le retour reçu encore non identifié.", "RETURN_QUANTITY", 409);
        var offer = await db.Offers.AsNoTracking().FirstAsync(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == source.Product);
        var allLots = await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == source.Product).ToListAsync();
        Check.That(offer.Quarantined - allLots.Sum(x => x.Quarantined) >= input.Quantity, "La quarantaine physique n'est pas disponible.", "STOCK_CONFLICT", 409);
        var originalAllocations = await db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == source.Order && x.ProductId == source.Product && x.Status == "DISPATCHED" && x.LotId != "").ToListAsync();
        var originalIds = originalAllocations.Select(x => x.LotId).ToList();
        if (originalIds.Count > 0)
        {
            var originals = await db.WarehouseLots.AsNoTracking().Where(x => originalIds.Contains(x.Id) && x.LotNumber == number).ToListAsync();
            // Preserve the exact dispatched batch identity used by recall holds;
            // accepting a different hour on the same date could create an unheld return lot.
            var identity = RecallGuard.Identity(source.Product, number, input.ExpiresAt);
            var matchingIds = originals.Where(x => RecallGuard.Identity(x.ProductId, x.LotNumber, x.ExpiresAt) == identity).Select(x => x.Id).ToList();
            Check.That(matchingIds.Count > 0, "Ce lot ou sa date ne correspond pas aux lots expédiés dans cette commande.", "LOT_MISMATCH", 422);
            var claimIds = await db.Claims.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == source.Order && x.ProductId == source.Product).Select(x => x.Id).ToListAsync();
            var lineIds = await db.Set<OrderLine>().AsNoTracking().Where(x => x.PurchaseOrderId == source.Order && x.ProductId == source.Product).Select(x => x.Id).ToListAsync();
            var giftIds = await db.PromotionSnapshots.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == source.Order && x.GiftProductId == source.Product).Select(x => x.Id).ToListAsync();
            var refs = claimIds.Select(x => "CLAIM:" + x).Concat(lineIds.Select(x => "REFUSAL:" + x)).Concat(giftIds.Select(x => "PROMOTION:" + x)).ToList();
            var orderReturns = await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == source.Product && refs.Contains(x.SourceRef) && x.LotNumber == number).ToListAsync();
            var alreadyIdentified = orderReturns.Where(x => RecallGuard.Identity(x.ProductId, x.LotNumber, x.ExpiresAt) == identity).Sum(x => x.InitialQuantity);
            Check.That(input.Quantity <= originalAllocations.Where(x => matchingIds.Contains(x.LotId)).Sum(x => x.Quantity) - alreadyIdentified, "La quantité déclarée pour ce lot dépasse la quantité expédiée encore non rapprochée.", "RETURN_LOT_QUANTITY", 409);
        }
        var lot = new WarehouseLot { WorkspaceId = a.WorkspaceId, ProductId = source.Product, LotNumber = number, ExpiresAt = input.ExpiresAt?.ToUniversalTime(), Quarantined = input.Quantity, InitialQuantity = input.Quantity, SourceKey = "RETURN:" + IdGen.New(), SourceRef = reference };
        db.WarehouseLots.Add(lot);
        Operation(a, input.IdempotencyKey, hash, "RETURN_LOT_IDENTIFIED", lot.Id, input.Quantity);
        Audit(a, "RETURN_LOT_IDENTIFIED", lot.Id, proof + " · " + reference);
        await db.SaveChangesAsync();
        return await Lots(a, source.Product);
    }
    public async Task InspectReturn(Actor a, string orderId, string product, string sourceType, string sourceId, int quantity, string disposition)
    {
        if (!await IsManaged(a.WorkspaceId, product))
            return;
        await LockProduct(a.WorkspaceId, product);
        var source = await ReturnSource(a, sourceType, sourceId);
        Check.That(source.Order == orderId && source.Product == product && quantity > 0 && quantity <= source.Remaining, "Quantité retournée modifiée.", "RETURN_QUANTITY", 409);
        var lots = (await db.WarehouseLots.Where(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == product && x.SourceRef == sourceType + ":" + sourceId && x.Quarantined > 0).ToListAsync()).OrderBy(x => x.ExpiresAt ?? DateTimeOffset.MaxValue).ToList();
        var restock = disposition is "RESTOCK" or "RELEASE";
        if (restock)
            foreach (var lot in lots) await RecallGuard.AssertLot(db, a.WorkspaceId, lot);
        if (restock)
            lots = lots.Where(x => x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow).ToList();
        if (restock)
            Check.That(lots.Sum(x => x.Quarantined) >= quantity, "Identifiez le lot retourné et sa date avant remise en stock. Les lots expirés restent isolés.", "RETURN_LOT_IDENTIFICATION_REQUIRED", 409);
        var remaining = quantity;
        foreach (var lot in lots)
        {
            var take = Math.Min(remaining, lot.Quarantined);
            lot.Quarantined -= take;
            if (restock)
                lot.OnHand += take;
            lot.Version++;
            remaining -= take;
            if (remaining == 0)
                break;
        }
        // A justified destruction may dispose of unidentified quarantine; it can never create sellable stock.
        Audit(a, "RETURN_LOT_" + (restock ? "RELEASED" : "DESTROYED"), sourceId, quantity.ToString());
    }
}
