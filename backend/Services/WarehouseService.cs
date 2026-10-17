using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace Toushir.Wholesale;

/// <summary>Opt-in counted lots and real scan tasks; untracked legacy stock is never assigned a fictitious manufacturer batch.</summary>
public sealed partial class WarehouseService(WholesaleDb db, RealtimeChanges changes)
{
    private static string Hash(object data) => Tokens.Hash(JsonSerializer.Serialize(data));
    private void Audit(Actor a, string kind, string id, string note = "") => db.Audit.Add(new() { WorkspaceId = a.WorkspaceId, ActorId = a.Id, Actor = a.Name, Action = kind, EntityId = id, Details = note });
    private void Changed(Actor a, PurchaseOrder o, string note)
    {
        changes.Order(o.WorkspaceId, o.Id, o.AccountId);
        o.Version++;
        o.UpdatedAt = DateTimeOffset.UtcNow;
        db.OrderEvents.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            OrderId = o.Id,
            Actor = a.Name,
            Action = "WAREHOUSE_SCAN_UPDATED",
            Details = note
        });
        db.EngagementNotices.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            MerchantId = o.AccountId,
            Kind = "PREPARATION_UPDATED",
            AggregateId = o.Id
        });
    }
    private async Task<WarehouseOperation?> Replay(Actor a, string key, string hash)
    {
        Check.Text(key, 100, "Clé d'idempotence");
        var old = await db.WarehouseOperations.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.ActorId == a.Id && x.IdempotencyKey == key);
        if (old != null)
            Check.That(old.RequestHash == hash, "Clé déjà utilisée pour une opération différente.", "IDEMPOTENCY_CONFLICT", 409);
        return old;
    }
    private void Operation(Actor a, string key, string hash, string kind, string result, int qty = 0) => db.WarehouseOperations.Add(new() { WorkspaceId = a.WorkspaceId, ActorId = a.Id, IdempotencyKey = key, RequestHash = hash, Kind = kind, ResultId = result, Quantity = qty });
    public Task<bool> IsManaged(string workspace, string product) => db.LotProductControls.AnyAsync(x => x.WorkspaceId == workspace && x.ProductId == product);
    public async Task<int?> EligibleAvailable(string workspace, string product)
    {
        if (!await IsManaged(workspace, product))
            return null;
        var lots = await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == workspace && x.ProductId == product).ToListAsync();
        return lots.Where(x => x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow).Sum(x => Math.Max(0, x.OnHand - x.Reserved));
    }
    public async Task AssertManualStockChange(string workspace, string product, int? newQuantity)
    {
        if (!await IsManaged(workspace, product))
            return;
        var current = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == workspace && x.ProductId == product).Select(x => x.OnHand).FirstOrDefaultAsync();
        Check.That(current == newQuantity, "Le stock est géré par lots. Utilisez le comptage de lot, la réception ou le contrôle qualité.", "LOT_CONTROL_REQUIRED", 409);
    }
    public async Task<object> Lots(Actor a, string? productId)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER", "FINANCE");
        var q = db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId);
        var c = db.LotProductControls.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId);
        if (!string.IsNullOrEmpty(productId))
        {
            q = q.Where(x => x.ProductId == productId);
            c = c.Where(x => x.ProductId == productId);
        }
        var selectedProduct = string.IsNullOrWhiteSpace(productId) ? null : await db.Products.AsNoTracking().Where(x => x.Id == productId && x.IsCatalogActive).Select(x => new { x.Id, x.Name, x.Barcode }).FirstOrDefaultAsync();
        var selectedStock = selectedProduct == null ? null : await db.Offers.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == productId);
        var lots = await q.ToListAsync();
        var ids = lots.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return new
        {
            product = selectedProduct,
            stock = selectedProduct == null ? null : new
            {
                onHand = selectedStock?.OnHand,
                reserved = selectedStock?.Reserved ?? 0,
                quarantined = selectedStock?.Quarantined ?? 0
            },
            controls = await c.ToListAsync(),
            items = lots.OrderBy(x => x.ExpiresAt ?? DateTimeOffset.MaxValue).ThenBy(x => x.ReceivedAt).Select(x => new { x.Id, x.ProductId, productName = products.GetValueOrDefault(x.ProductId)?.Name, barcode = products.GetValueOrDefault(x.ProductId)?.Barcode, x.LotNumber, x.ExpiresAt, x.OnHand, x.Reserved, x.Quarantined, x.InitialQuantity, x.SourceKey, x.SourceRef, x.Version, expired = x.ExpiresAt <= DateTimeOffset.UtcNow, eligibleAvailable = x.ExpiresAt <= DateTimeOffset.UtcNow ? 0 : Math.Max(0, x.OnHand - x.Reserved) }),
            policy = "FEFO: date d'expiration la plus proche, puis réception la plus ancienne; lots sans date en dernier."
        };
    }
    public async Task<object> Activate(Actor a, ActivateLotsInput input)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE");
        Check.That(input.Lots is { Count: <= 200 } && input.Lots.All(x => x != null && !string.IsNullOrWhiteSpace(x.LotNumber)) && input.Lots.Select(x => x.LotNumber.Trim()).Distinct().Count() == input.Lots.Count, "Maximum 200 lots distincts.");
        var reason = Check.Text(input.Reason, 1000, "Motif");
        var proof = Check.Text(input.ProofReference, 300, "Référence de comptage");
        var hash = Hash(new
        {
            kind = "ACTIVATE",
            input.ProductId,
            input.ExpectedPhysicalStock,
            reason,
            proof,
            input.Lots
        });
        if (await Replay(a, input.IdempotencyKey, hash) != null)
            return await Lots(a, input.ProductId);
        Check.That(!await IsManaged(a.WorkspaceId, input.ProductId), "Ce produit est déjà géré par lots. Comptez ses lots individuellement.", "ALREADY_LOT_MANAGED", 409);
        Check.That(await db.Products.AnyAsync(x => x.Id == input.ProductId && x.IsCatalogActive), "Produit introuvable.", "NOT_FOUND", 404);
        foreach (var lot in input.Lots)
        {
            Check.Text(lot.LotNumber, 100, "Lot");
            Check.That(lot.Quantity >= 0 && lot.Quarantined >= 0 && lot.Quantity <= 1000000 && lot.Quarantined <= 1000000, "Comptage invalide.");
            Check.That(lot.ExpiresAt == null || lot.ExpiresAt > DateTimeOffset.UtcNow || lot.Quantity == 0, "Un lot expiré ne peut pas avoir de stock vendable.");
        }
        var total = input.Lots.Sum(x => (long)x.Quantity);
        var quarantine = input.Lots.Sum(x => (long)x.Quarantined);
        Check.That(total <= 1000000000 && quarantine <= 1000000000, "Comptage trop important.");
        var offer = await db.Offers.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == input.ProductId);
        if (offer == null)
        {
            offer = new()
            {
                WorkspaceId = a.WorkspaceId,
                ProductId = input.ProductId
            };
            db.Offers.Add(offer);
            await db.SaveChangesAsync();
        }
        Check.That(offer.Quarantined == 0, "Terminez le contrôle des anciennes quarantaines avant d'activer les lots.", "LEGACY_QUARANTINE_OPEN", 409);
        var old = offer.OnHand;
        db.Entry(offer).State = EntityState.Detached;
        var expected = input.ExpectedPhysicalStock ?? -1;
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"OnHand\"={(int)total}, \"Quarantined\"={(int)quarantine}, \"UpdatedAt\"={DateTimeOffset.UtcNow} WHERE \"WorkspaceId\"={a.WorkspaceId} AND \"ProductId\"={input.ProductId} AND \"Reserved\"=0 AND \"Quarantined\"=0 AND COALESCE(\"OnHand\",-1)={expected}");
        Check.That(changed == 1, "Stock modifié ou réservé depuis le comptage. Actualisez et recomptez.", "STOCK_VERSION_CONFLICT", 409);
        var control = new LotProductControl { WorkspaceId = a.WorkspaceId, ProductId = input.ProductId, ActivatedBy = a.Id, CountReference = proof };
        db.LotProductControls.Add(control);
        for (var i = 0; i < input.Lots.Count; i++)
        {
            var l = input.Lots[i];
            db.WarehouseLots.Add(new()
            {
                WorkspaceId = a.WorkspaceId,
                ProductId = input.ProductId,
                LotNumber = l.LotNumber.Trim(),
                ExpiresAt = l.ExpiresAt?.ToUniversalTime(),
                OnHand = l.Quantity,
                Quarantined = l.Quarantined,
                InitialQuantity = l.Quantity + l.Quarantined,
                SourceKey = "COUNT:" + control.Id + ":" + i,
                SourceRef = proof
            });
        }
        db.Inventory.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            ProductId = input.ProductId,
            Kind = "COUNTED_LOTS_ACTIVATED",
            Delta = checked((int)total - (old ?? 0)),
            QuarantineDelta = (int)quarantine,
            Actor = a.Name,
            Reason = reason + " · " + proof
        });
        Operation(a, input.IdempotencyKey, hash, "ACTIVATE_LOTS", control.Id);
        Audit(a, "COUNTED_LOTS_ACTIVATED", control.Id, reason + " · " + proof);
        await db.SaveChangesAsync();
        return await Lots(a, input.ProductId);
    }
    private async Task Allocate(Actor a, PurchaseOrder o, string product, int quantity, string allocationKey, string lineId = "", string promotionId = "")
    {
        if (quantity == 0)
            return;
        if (await db.LotAllocations.AnyAsync(x => x.OrderId == o.Id && x.AllocationKey == allocationKey) || db.LotAllocations.Local.Any(x => x.OrderId == o.Id && x.AllocationKey == allocationKey))
            return;
        if (!await IsManaged(a.WorkspaceId, product))
        {
            db.LotAllocations.Add(new()
            {
                WorkspaceId = a.WorkspaceId,
                OrderId = o.Id,
                ProductId = product,
                AllocationKey = allocationKey,
                OrderLineId = lineId,
                PromotionSnapshotId = promotionId,
                Quantity = quantity
            });
            return;
        }
        var lots = (await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == product && x.OnHand > x.Reserved).ToListAsync()).Where(x => x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow).OrderBy(x => x.ExpiresAt ?? DateTimeOffset.MaxValue).ThenBy(x => x.ReceivedAt).ThenBy(x => x.Id).ToList();
        var remaining = quantity;
        foreach (var lot in lots)
        {
            var take = Math.Min(remaining, lot.OnHand - lot.Reserved);
            if (take == 0)
                continue;
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WarehouseLots\" SET \"Reserved\"=\"Reserved\"+{take}, \"Version\"=\"Version\"+1 WHERE \"Id\"={lot.Id} AND \"WorkspaceId\"={a.WorkspaceId} AND \"Version\"={lot.Version} AND \"OnHand\"-\"Reserved\">={take}");
            Check.That(changed == 1, "Un lot a été réservé simultanément. Réessayez avec la nouvelle disponibilité.", "LOT_STOCK_CONFLICT", 409);
            db.LotAllocations.Add(new()
            {
                WorkspaceId = a.WorkspaceId,
                OrderId = o.Id,
                ProductId = product,
                LotId = lot.Id,
                AllocationKey = allocationKey,
                OrderLineId = lineId,
                PromotionSnapshotId = promotionId,
                Quantity = take
            });
            Audit(a, "LOT_RESERVED_FEFO", o.Id, $"{lot.LotNumber}: {take}");
            remaining -= take;
            if (remaining == 0)
                break;
        }
        Check.That(remaining == 0, "Les lots non expirés ne couvrent pas la quantité. Aucune réservation partielle n'est conservée.", "LOT_STOCK_INSUFFICIENT", 409);
    }
    public async Task ReserveOrder(Actor a, PurchaseOrder o)
    {
        foreach (var line in o.Items.Where(x => x.Quantity > 0))
            await Allocate(a, o, line.ProductId, line.Quantity, "LINE:" + line.Id, lineId: line.Id);
    }
    public Task ReserveGift(Actor a, PurchaseOrder o, PromotionSnapshot p) => Allocate(a, o, p.GiftProductId, p.GiftQuantity, "GIFT:" + p.Id, promotionId: p.Id);
    private async Task<List<LotAllocation>> Allocations(PurchaseOrder o) => await db.LotAllocations.Where(x => x.WorkspaceId == o.WorkspaceId && x.OrderId == o.Id).ToListAsync();
    public async Task ReleaseOrder(Actor a, PurchaseOrder o)
    {
        foreach (var allocation in (await Allocations(o)).Where(x => x.Status == "RESERVED"))
        {
            if (allocation.LotId.Length > 0)
            {
                var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WarehouseLots\" SET \"Reserved\"=\"Reserved\"-{allocation.Quantity}, \"Version\"=\"Version\"+1 WHERE \"Id\"={allocation.LotId} AND \"WorkspaceId\"={a.WorkspaceId} AND \"Reserved\">={allocation.Quantity}");
                Check.That(changed == 1, "Réserve de lot incohérente.", "LOT_STOCK_CONFLICT", 409);
            }
            allocation.Status = "RELEASED";
            allocation.Version++;
            Audit(a, "LOT_RESERVATION_RELEASED", allocation.Id);
        }
    }
    public async Task<bool> ScansComplete(PurchaseOrder o)
    {
        var allocations = await db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == o.WorkspaceId && x.OrderId == o.Id && x.Status == "RESERVED").ToListAsync();
        return allocations.Where(x => x.LotId.Length > 0 || x.WaveId.Length > 0).All(x => x.PickedQuantity == x.Quantity);
    }
    public async Task ValidatePreparation(Actor a, PurchaseOrder o, List<PrepareLineInput> input)
    {
        var allocations = await Allocations(o);
        foreach (var item in input)
        {
            var line = o.Items.FirstOrDefault(x => x.ProductId == item.ProductId);
            if (line == null)
                continue;
            var tasks = allocations.Where(x => x.OrderLineId == line.Id && x.Status == "RESERVED").ToList();
            if (tasks.Any(x => x.LotId.Length > 0 || x.WaveId.Length > 0))
                Check.That(item.PreparedQuantity <= tasks.Sum(x => x.PickedQuantity), "Validez les scans code-barres et lots avant de confirmer cette quantité.", "SCAN_REQUIRED", 409);
        }
    }
    public async Task DispatchOrder(Actor a, PurchaseOrder o)
    {
        Check.That(await ScansComplete(o), "Les scans de préparation ne sont pas terminés.", "SCAN_REQUIRED", 409);
        foreach (var allocation in (await Allocations(o)).Where(x => x.Status == "RESERVED"))
        {
            if (allocation.LotId.Length > 0)
            {
                var lot = await db.WarehouseLots.AsNoTracking().FirstAsync(x => x.Id == allocation.LotId);
                Check.That(lot.ExpiresAt == null || lot.ExpiresAt > DateTimeOffset.UtcNow, "Un lot a expiré avant le départ. Réservez une nouvelle version.", "LOT_EXPIRED", 409);
                var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"WarehouseLots\" SET \"OnHand\"=\"OnHand\"-{allocation.Quantity}, \"Reserved\"=\"Reserved\"-{allocation.Quantity}, \"Version\"=\"Version\"+1 WHERE \"Id\"={allocation.LotId} AND \"WorkspaceId\"={a.WorkspaceId} AND \"OnHand\">={allocation.Quantity} AND \"Reserved\">={allocation.Quantity}");
                Check.That(changed == 1, "Le lot réservé ne peut pas être expédié.", "LOT_STOCK_CONFLICT", 409);
            }
            allocation.Status = "DISPATCHED";
            allocation.Version++;
            Audit(a, "LOT_DISPATCHED", allocation.Id, allocation.Quantity.ToString());
        }
    }
    public async Task<object> Waves(Actor a)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER");
        return new
        {
            items = (await db.PickWaves.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync()).OrderByDescending(x => x.At)
        };
    }
    public async Task<object> Wave(Actor a, string id)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER");
        var wave = await db.PickWaves.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == a.WorkspaceId);
        Check.That(wave != null, "Vague introuvable.", "NOT_FOUND", 404);
        var tasks = await db.LotAllocations.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.WaveId == id).ToListAsync();
        var productIds = tasks.Select(x => x.ProductId).Distinct().ToList();
        var lotIds = tasks.Select(x => x.LotId).Where(x => x.Length > 0).ToList();
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var lots = await db.WarehouseLots.AsNoTracking().Where(x => lotIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return new
        {
            wave,
            items = tasks.Select(t => new { t.Id, t.OrderId, t.OrderLineId, t.PromotionSnapshotId, t.ProductId, productName = products[t.ProductId].Name, barcode = products[t.ProductId].Barcode, t.LotId, lotNumber = lots.GetValueOrDefault(t.LotId)?.LotNumber, expiresAt = lots.GetValueOrDefault(t.LotId)?.ExpiresAt, t.Quantity, t.PickedQuantity, t.Status, t.Version, fefoManaged = t.LotId.Length > 0, isGift = t.PromotionSnapshotId.Length > 0 }),
            total = tasks.Sum(x => x.Quantity),
            picked = tasks.Sum(x => x.PickedQuantity)
        };
    }
    public async Task<object> CreateWave(Actor a, CreatePickWaveInput input)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE");
        Check.That(input.OrderIds is { Count: > 0 and <= 100 } && input.OrderIds.Distinct().Count() == input.OrderIds.Count, "Choisissez des commandes distinctes.");
        var name = Check.Text(input.Name, 120, "Nom");
        var hash = Hash(new
        {
            kind = "WAVE",
            name,
            orders = input.OrderIds.Order().ToList()
        });
        var old = await Replay(a, input.IdempotencyKey, hash);
        if (old != null)
            return await Wave(a, old.ResultId);
        var list = await db.Orders.Include(x => x.Items).Where(x => x.WorkspaceId == a.WorkspaceId && input.OrderIds.Contains(x.Id)).ToListAsync();
        Check.That(list.Count == input.OrderIds.Count && list.All(x => x.Status is "CONFIRMED" or "PREPARING" or "READY"), "Les commandes doivent être confirmées avant départ.", "INVALID_TRANSITION", 409);
        // Existing untracked orders can opt into real barcode tasks without being relabeled as known lots.
        foreach (var order in list)
            await ReserveOrder(a, order);
        var ids = list.Select(x => x.Id).ToList();
        var gifts = await db.PromotionSnapshots.Where(x => x.WorkspaceId == a.WorkspaceId && ids.Contains(x.OrderId) && x.GiftStockState == "RESERVED" && x.GiftQuantity > 0).ToListAsync();
        foreach (var gift in gifts.Where(x => list.Any(o => o.Id == x.OrderId && o.QuoteVersion == x.QuoteVersion)))
            await ReserveGift(a, list.First(o => o.Id == gift.OrderId), gift);
        await db.SaveChangesAsync();
        var tasks = await db.LotAllocations.Where(x => x.WorkspaceId == a.WorkspaceId && input.OrderIds.Contains(x.OrderId) && x.Status == "RESERVED").ToListAsync();
        Check.That(tasks.Count > 0 && tasks.All(x => x.WaveId.Length == 0), "Une tâche appartient déjà à une vague.", "WAVE_CONFLICT", 409);
        var wave = new PickWave { WorkspaceId = a.WorkspaceId, Name = name, CreatedBy = a.Id };
        db.PickWaves.Add(wave);
        foreach (var task in tasks)
        {
            task.WaveId = wave.Id;
            task.PickedQuantity = 0;
            task.Version++;
        }
        foreach (var o in list)
        {
            foreach (var line in o.Items)
                line.PreparedQuantity = 0;
            o.Status = "PREPARING";
            Changed(a, o, "Vague " + name + " ouverte; scans requis.");
        }
        Operation(a, input.IdempotencyKey, hash, "WAVE_CREATED", wave.Id);
        Audit(a, "PICK_WAVE_CREATED", wave.Id);
        await db.SaveChangesAsync();
        return await Wave(a, wave.Id);
    }
    public async Task<object> Scan(Actor a, string taskId, ScanPickInput input)
    {
        a.RequireRoles("ADMIN", "WAREHOUSE");
        var barcode = Check.Text(input.Barcode, 100, "Code-barres");
        var lotNumber = Check.Text(input.LotNumber, 100, "Lot", false);
        var hash = Hash(new
        {
            kind = "SCAN",
            taskId,
            barcode,
            lotNumber,
            input.Quantity
        });
        var previous = await Replay(a, input.IdempotencyKey, hash);
        if (previous != null)
            return await Wave(a, previous.ResultId);
        var task = await db.LotAllocations.FirstOrDefaultAsync(x => x.Id == taskId && x.WorkspaceId == a.WorkspaceId);
        Check.That(task != null && task.WaveId.Length > 0, "Tâche introuvable.", "NOT_FOUND", 404);
        Check.That(task.Version == input.Version, "La tâche a évolué.", "VERSION_CONFLICT", 409);
        Check.That(task.Status == "RESERVED" && input.Quantity > 0 && input.Quantity <= task.Quantity - task.PickedQuantity, "Quantité à scanner invalide.");
        var order = await db.Orders.Include(x => x.Items).FirstAsync(x => x.Id == task.OrderId && x.WorkspaceId == a.WorkspaceId);
        Check.That(order.Status is "CONFIRMED" or "PREPARING" or "READY", "Le départ a déjà eu lieu.", "INVALID_TRANSITION", 409);
        var product = await db.Products.AsNoTracking().FirstAsync(x => x.Id == task.ProductId);
        Check.That(barcode == product.Barcode, "Ce code-barres ne correspond pas au produit attendu.", "BARCODE_MISMATCH", 422);
        if (task.LotId.Length > 0)
        {
            var lot = await db.WarehouseLots.AsNoTracking().FirstAsync(x => x.Id == task.LotId);
            Check.That(lot.LotNumber == lotNumber, "Le lot ne correspond pas à la réservation FEFO.", "LOT_MISMATCH", 422);
            Check.That(lot.ExpiresAt == null || lot.ExpiresAt > DateTimeOffset.UtcNow, "Le lot a expiré. Ne le préparez pas.", "LOT_EXPIRED", 422);
        }
        task.PickedQuantity += input.Quantity;
        task.Version++;
        Operation(a, input.IdempotencyKey, hash, "PICK_SCANNED", task.WaveId, input.Quantity);
        Audit(a, "PICK_SCANNED", task.Id, barcode + " · " + lotNumber + " · " + input.Quantity);
        await db.SaveChangesAsync();
        var allocations = await Allocations(order);
        foreach (var line in order.Items)
            line.PreparedQuantity = allocations.Where(x => x.OrderLineId == line.Id && x.Status == "RESERVED").Sum(x => x.PickedQuantity);
        order.Status = order.Items.All(x => x.PreparedQuantity == x.Quantity) && await ScansComplete(order) ? "READY" : "PREPARING";
        Changed(a, order, $"{order.Items.Sum(x => x.PreparedQuantity)}/{order.Items.Sum(x => x.Quantity)} unités vérifiées par scan.");
        var wave = await db.PickWaves.FirstAsync(x => x.Id == task.WaveId);
        var waveTasks = await db.LotAllocations.Where(x => x.WaveId == task.WaveId).ToListAsync();
        if (waveTasks.All(x => x.PickedQuantity == x.Quantity))
            wave.Status = "COMPLETE";
        await db.SaveChangesAsync();
        return await Wave(a, task.WaveId);
    }
}
