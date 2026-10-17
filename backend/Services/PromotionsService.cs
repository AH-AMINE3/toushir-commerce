using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Frozen quote terms and a separately accounted free-goods custody chain. This service never changes an accepted unit price.</summary>
public sealed class PromotionsService(WholesaleDb db)
{
    private static long Minor(decimal value) => checked((long)decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
    private void Audit(Actor a, string action, string id, string note = "") => db.Audit.Add(new() { WorkspaceId = a.WorkspaceId, ActorId = a.Id, Actor = a.Name, Action = action, EntityId = id, Details = note });
    private void Ledger(Actor a, PromotionSnapshot p, string kind, int physical = 0, int reserved = 0, int quarantine = 0) => db.Inventory.Add(new() { WorkspaceId = a.WorkspaceId, ProductId = p.GiftProductId, Kind = kind, Delta = physical, ReservedDelta = reserved, QuarantineDelta = quarantine, OrderId = p.OrderId, Actor = a.Name, Reason = p.CampaignTitle });
    private void Event(Actor a, PromotionSnapshot p, string kind, string key, int quantity = 0, long discount = 0, string note = "", string hash = "") => db.PromotionEvents.Add(new() { WorkspaceId = a.WorkspaceId, PromotionSnapshotId = p.Id, Kind = kind, IdempotencyKey = key, RequestHash = hash, Quantity = quantity, DiscountMinor = discount, ActorId = a.Id, Note = note });
    private async Task<PurchaseOrder> Order(Actor actor, string id)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId); Check.That(o != null, "Commande introuvable.", "NOT_FOUND", 404);
        if (actor.Role == "MERCHANT") Check.That(o.AccountId == actor.Id, "Commande introuvable.", "NOT_FOUND", 404);
        else if (actor.Role == "DRIVER") Check.That(await db.DeliveryAssignments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == id && x.DriverId == actor.Id), "Commande introuvable.", "NOT_FOUND", 404);
        else actor.RequireRoles("ADMIN", "FINANCE", "WAREHOUSE", "DISPATCHER"); return o;
    }
    public async Task<object> View(Actor actor, string orderId)
    {
        await Order(actor, orderId); var items = await db.PromotionSnapshots.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == orderId).ToListAsync();
        var ids = items.Select(x => x.Id).ToList(); var events = await db.PromotionEvents.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && ids.Contains(x.PromotionSnapshotId)).ToListAsync();
        return new { items = items.OrderByDescending(x => x.QuoteVersion), events = events.OrderBy(x => x.At), selectionPolicy = "Une promotion par produit; plus forte remise d'abord, puis gratuité, puis identifiant stable. Aucun cumul implicite.", monetaryUnit = "CENTIME_FOR_DISCOUNT_TOTALS", currency = "DZD" };
    }
    public async Task<List<QuoteLineInput>> ApplyQuote(Actor actor, PurchaseOrder o, List<QuoteLineInput> lines, int? quoteVersion = null, IReadOnlyList<string>? campaignIds = null)
    {
        var version = quoteVersion ?? o.QuoteVersion + 1; var old = await db.PromotionSnapshots.Where(x => x.OrderId == o.Id && x.WorkspaceId == actor.WorkspaceId && x.Status == "QUOTED").ToListAsync();
        foreach (var p in old) { p.Status = "SUPERSEDED"; p.Version++; Event(actor, p, "QUOTE_SUPERSEDED", "superseded:" + version); }
        var now = DateTimeOffset.UtcNow; var campaigns = (await db.Campaigns.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.Status == "PUBLISHED" && x.Kind != "CHALLENGE").ToListAsync()).Where(x => x.StartsAt <= now && now <= x.EndsAt).ToList();
        if (campaignIds != null)
        {
            Check.That(campaignIds.Count <= 100 && campaignIds.Distinct().Count() == campaignIds.Count, "Choix de campagnes invalide.");
            Check.That(campaignIds.All(id => campaigns.Any(c => c.Id == id && lines.Any(l => l.ProductId == c.ProductId && l.Quantity >= c.MinimumQuantity))), "Une campagne sélectionnée est expirée, hors périmètre ou sous le seuil.", "CAMPAIGN_NOT_APPLICABLE", 409);
            campaigns = campaigns.Where(c => campaignIds.Contains(c.Id)).ToList();
            Check.That(campaigns.GroupBy(c => c.ProductId).All(g => g.Count() <= 1), "Choisissez une seule mécanique par produit. Aucun cumul implicite.", "CAMPAIGN_COMBINATION", 409);
        }
        var result = new List<QuoteLineInput>();
        foreach (var q in lines)
        {
            Check.Money(q.UnitPrice, "Prix de base");
            var c = campaigns.Where(x => x.ProductId == q.ProductId && q.Quantity >= x.MinimumQuantity).OrderByDescending(x => x.Kind == "DISCOUNT" ? x.DiscountPercent : -1m).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (c == null) { result.Add(q); continue; }
            var applied = c.Kind == "DISCOUNT" ? decimal.Round(q.UnitPrice * (100m - c.DiscountPercent) / 100m, 2, MidpointRounding.AwayFromZero) : q.UnitPrice;
            Check.That(applied > 0 || q.Quantity == 0, "La remise produit un prix nul. Utilisez une gratuité explicite.", "PROMOTION_PRICE_INVALID", 409);
            var snapshot = new PromotionSnapshot { WorkspaceId = actor.WorkspaceId, OrderId = o.Id, QuoteVersion = version, CampaignId = c.Id, CampaignVersion = c.Version, CampaignTitle = c.Title, Kind = c.Kind, ProductId = q.ProductId, QuotedQuantity = q.Quantity, MinimumQuantity = c.MinimumQuantity, BaseUnitPrice = q.UnitPrice, AppliedUnitPrice = applied, DiscountPercent = c.DiscountPercent, QuotedDiscountMinor = Minor((q.UnitPrice - applied) * q.Quantity), EffectiveDiscountMinor = Minor((q.UnitPrice - applied) * q.Quantity) };
            if (c.Kind == "FREE_GOODS")
            {
                var gift = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.GiftProductId && x.IsCatalogActive); Check.That(gift != null, "Le produit offert n'est plus disponible au catalogue.", "GIFT_UNAVAILABLE", 409);
                snapshot.GiftProductId = gift.Id; snapshot.GiftProductName = gift.Name; snapshot.GiftQuantity = c.GiftQuantity; snapshot.GiftStockState = "QUOTED";
            }
            db.PromotionSnapshots.Add(snapshot); Event(actor, snapshot, "PROMOTION_QUOTED", "quoted", q.Quantity, snapshot.QuotedDiscountMinor, "Conditions figées de la campagne version " + c.Version);
            Audit(actor, "PROMOTION_APPLIED_TO_QUOTE", o.Id, $"{c.Id}/v{c.Version}; produit {q.ProductId}; base {q.UnitPrice}; retenu {applied}; devis {version}"); result.Add(q with { UnitPrice = applied });
        }
        return result;
    }
    public async Task ApplyDirectOrder(Actor actor, PurchaseOrder o)
    {
        var lines = await ApplyQuote(actor, o, o.Items.Select(x => new QuoteLineInput(x.ProductId, x.Quantity, x.UnitPrice ?? 0, x.CaseSize, null)).ToList(), o.QuoteVersion);
        foreach (var line in o.Items) { var q = lines.First(x => x.ProductId == line.ProductId); line.UnitPrice = q.UnitPrice; line.LineTotal = q.UnitPrice * line.Quantity; }
    }
    private async Task<List<PromotionSnapshot>> Current(PurchaseOrder o) => await db.PromotionSnapshots.Where(x => x.OrderId == o.Id && x.WorkspaceId == o.WorkspaceId && x.QuoteVersion == o.QuoteVersion && x.Status != "SUPERSEDED").ToListAsync();
    public async Task Reserve(Actor actor, PurchaseOrder o)
    {
        // Direct-order snapshots can still be tracked Added when this method is reached.
        var current = await Current(o); current.AddRange(db.PromotionSnapshots.Local.Where(x => x.OrderId == o.Id && x.QuoteVersion == o.QuoteVersion && db.Entry(x).State == EntityState.Added && !current.Contains(x)));
        foreach (var p in current.Where(x => x.Status == "QUOTED"))
        {
            if (p.GiftQuantity > 0)
            {
                var qty = p.GiftQuantity;
                var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"Reserved\"=\"Reserved\"+{qty} WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"ProductId\"={p.GiftProductId} AND \"IsActive\"={true} AND \"OnHand\" IS NOT NULL AND \"OnHand\"-\"Reserved\">={qty}");
                Check.That(changed == 1, "Le cadeau promis est indisponible. Le grossiste doit revoir la proposition.", "GIFT_STOCK_CONFLICT", 409); p.GiftStockState = "RESERVED"; Ledger(actor, p, "PROMOTION_GIFT_RESERVED", reserved: qty);
            }
            p.Status = "ACCEPTED"; p.Version++; Event(actor, p, "PROMOTION_ACCEPTED", "accepted", p.QuotedQuantity, p.QuotedDiscountMinor);
        }
    }
    public async Task Release(Actor actor, PurchaseOrder o)
    {
        foreach (var p in await Current(o))
        {
            if (p.Status is "CANCELLED" or "SUPERSEDED") continue;
            if (p.GiftStockState == "RESERVED")
            {
                var qty = p.GiftQuantity; var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"Reserved\"=\"Reserved\"-{qty} WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"ProductId\"={p.GiftProductId} AND \"Reserved\">={qty}");
                Check.That(changed == 1, "Réserve de cadeau incohérente.", "INVENTORY_CONFLICT", 409); Ledger(actor, p, "PROMOTION_GIFT_RELEASED", reserved: -qty); p.GiftStockState = "RELEASED";
            }
            Check.That(p.GiftStockState is not "IN_TRANSIT" and not "RETURN_REQUIRED", "Le cadeau est sous garde et doit revenir physiquement.", "CUSTODY_OPEN", 409);
            p.Status = "CANCELLED"; p.EffectiveDiscountMinor = 0; p.Version++; Event(actor, p, "PROMOTION_CANCELLED", "cancelled", discount: -p.QuotedDiscountMinor);
        }
    }
    public async Task Dispatch(Actor actor, PurchaseOrder o)
    {
        foreach (var p in (await Current(o)).Where(x => x.Status == "ACCEPTED"))
        {
            if (p.GiftStockState == "RESERVED")
            {
                var qty = p.GiftQuantity; var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"OnHand\"=\"OnHand\"-{qty}, \"Reserved\"=\"Reserved\"-{qty} WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"ProductId\"={p.GiftProductId} AND \"Reserved\">={qty} AND \"OnHand\">={qty}");
                Check.That(changed == 1, "Le cadeau réservé ne peut pas être chargé.", "INVENTORY_CONFLICT", 409); p.GiftStockState = "IN_TRANSIT"; Ledger(actor, p, "PROMOTION_GIFT_DISPATCHED", physical: -qty, reserved: -qty);
            }
            p.Status = "DISPATCHED"; p.Version++; Event(actor, p, "PROMOTION_DISPATCHED", "dispatched", p.GiftQuantity);
        }
    }
    public async Task RecordDelivery(Actor actor, PurchaseOrder o)
    {
        foreach (var p in (await Current(o)).Where(x => x.Status == "DISPATCHED"))
        {
            var line = o.Items.First(x => x.ProductId == p.ProductId); p.AcceptedTriggerQuantity = line.AcceptedQuantity;
            var acceptedDiscount = Minor((p.BaseUnitPrice - p.AppliedUnitPrice) * line.AcceptedQuantity); var reversed = p.EffectiveDiscountMinor - acceptedDiscount; p.EffectiveDiscountMinor = acceptedDiscount;
            if (p.GiftQuantity > 0 && line.AcceptedQuantity < p.MinimumQuantity) { p.GiftStockState = "RETURN_REQUIRED"; p.GiftRefusedQuantity = p.GiftQuantity; }
            p.Status = "DELIVERED"; p.Version++; Event(actor, p, "PROMOTION_DELIVERY_RECONCILED", "delivery", line.AcceptedQuantity, -reversed, "Le prix unitaire accepté reste figé; les refus annulent seulement leur avantage.");
        }
    }
    public async Task ReconcileReturns(Actor actor, PurchaseOrder o)
    {
        var claims = await db.Claims.AsNoTracking().Where(x => x.WorkspaceId == o.WorkspaceId && x.OrderId == o.Id).ToListAsync();
        foreach (var p in (await Current(o)).Where(x => x.Status == "DELIVERED"))
        {
            var returned = Math.Min(p.AcceptedTriggerQuantity, claims.Where(x => x.ProductId == p.ProductId).Sum(x => x.ReceivedQuantity)); if (returned == p.ReturnedTriggerQuantity) continue;
            var delta = returned - p.ReturnedTriggerQuantity; var discount = Minor((p.BaseUnitPrice - p.AppliedUnitPrice) * (p.AcceptedTriggerQuantity - returned)); var reversed = p.EffectiveDiscountMinor - discount;
            p.ReturnedTriggerQuantity = returned; p.EffectiveDiscountMinor = discount;
            if (p.GiftQuantity > 0 && p.AcceptedTriggerQuantity - returned < p.MinimumQuantity)
            {
                p.EligibilityReviewRequired = true;
                if (p.GiftStockState == "IN_TRANSIT") { p.GiftStockState = "RETURN_REQUIRED"; p.GiftRefusedQuantity = p.GiftQuantity; }
            }
            p.Version++; Event(actor, p, "PROMOTION_RETURN_RECONCILED", "return:" + returned, delta, -reversed, "Un cadeau déjà remis impose un examen; aucune dette ou reprise physique inventée."); Audit(actor, "PROMOTION_RETURN_RECONCILED", p.Id);
        }
    }
    private async Task<(PurchaseOrder Order, PromotionSnapshot Promotion)> Gift(Actor actor, string orderId, string snapshotId)
    {
        var o = await Order(actor, orderId); var p = await db.PromotionSnapshots.FirstOrDefaultAsync(x => x.Id == snapshotId && x.OrderId == orderId && x.WorkspaceId == actor.WorkspaceId && x.QuoteVersion == o.QuoteVersion && x.GiftQuantity > 0);
        Check.That(p != null, "Gratuité introuvable.", "NOT_FOUND", 404); return (o, p);
    }
    private async Task<bool> Replay(PromotionSnapshot p, string key, string hash)
    {
        var previous = await db.PromotionEvents.AsNoTracking().FirstOrDefaultAsync(x => x.PromotionSnapshotId == p.Id && x.IdempotencyKey == key);
        if (previous == null) return false; Check.That(previous.RequestHash == hash, "Clé déjà utilisée pour une autre preuve.", "IDEMPOTENCY_CONFLICT", 409); return true;
    }
    public async Task<object> GiftReceipt(Actor actor, string orderId, string id, PromotionGiftReceiptInput input)
    {
        actor.RequireRoles("ADMIN", "DRIVER"); var (o, p) = await Gift(actor, orderId, id); var key = Check.Text(input.IdempotencyKey, 100, "Clé"); var proof = Check.Text(input.ProofReference, 300, "Preuve");
        var hash = Tokens.Hash(JsonSerializer.Serialize(new { input.AcceptedQuantity, input.RefusedQuantity, proof })); if (await Replay(p, key, hash)) return p;
        Check.That(p.Version == input.Version, "La version a changé.", "VERSION_CONFLICT", 409); Check.That(p.Status == "DELIVERED" && p.GiftStockState is "IN_TRANSIT" or "RETURN_REQUIRED", "La gratuité n'est pas à remettre.", "INVALID_TRANSITION", 409);
        Check.That(input.AcceptedQuantity >= 0 && input.RefusedQuantity >= 0 && input.AcceptedQuantity + input.RefusedQuantity == p.GiftQuantity, "Quantités de gratuité invalides.");
        Check.That(input.AcceptedQuantity == 0 || !p.EligibilityReviewRequired && p.AcceptedTriggerQuantity - p.ReturnedTriggerQuantity >= p.MinimumQuantity && o.Total != null && o.Collected >= o.Total, "Les marchandises éligibles doivent être acceptées et réglées avant le cadeau.", "GIFT_NOT_ELIGIBLE", 409);
        p.GiftAcceptedQuantity = input.AcceptedQuantity; p.GiftRefusedQuantity = input.RefusedQuantity; p.GiftProofReference = proof; p.GiftStockState = input.RefusedQuantity > 0 ? "RETURN_REQUIRED" : "DELIVERED"; p.Version++;
        Event(actor, p, "PROMOTION_GIFT_RECEIPT", key, input.AcceptedQuantity, note: proof, hash: hash); Audit(actor, "PROMOTION_GIFT_RECEIPT", id); await db.SaveChangesAsync(); return p;
    }
    public async Task<object> GiftReturn(Actor actor, string orderId, string id, PromotionGiftDispositionInput input, bool inspection)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE"); var (_, p) = await Gift(actor, orderId, id); var key = Check.Text(input.IdempotencyKey, 100, "Clé"); var reason = Check.Text(input.Reason, 1000, "Motif"); var disposition = Check.Text(input.Disposition, 30, "Disposition").ToUpperInvariant();
        var hash = Tokens.Hash(JsonSerializer.Serialize(new { inspection, disposition, reason })); if (await Replay(p, key, hash)) return p;
        Check.That(p.Version == input.Version, "La version a changé.", "VERSION_CONFLICT", 409); var qty = p.GiftRefusedQuantity; Check.That(qty > 0, "Aucune quantité refusée à recevoir.");
        if (!inspection)
        {
            Check.That(disposition == "QUARANTINE" && p.GiftStockState == "RETURN_REQUIRED", "La gratuité doit être sous garde et refusée.", "INVALID_TRANSITION", 409);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"Quarantined\"=\"Quarantined\"+{qty} WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"ProductId\"={p.GiftProductId}"); Check.That(changed == 1, "Stock introuvable.", "INVENTORY_CONFLICT", 409);
            p.GiftStockState = "RETURNED_QUARANTINE"; Ledger(actor, p, "PROMOTION_GIFT_RETURNED", quarantine: qty);
        }
        else
        {
            Check.That(p.GiftStockState == "RETURNED_QUARANTINE" && disposition is "RELEASE" or "DESTROY", "Une inspection de la gratuité reçue est requise.", "INVALID_TRANSITION", 409); var delta = disposition == "RELEASE" ? qty : 0;
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Offers\" SET \"Quarantined\"=\"Quarantined\"-{qty}, \"OnHand\"=COALESCE(\"OnHand\",0)+{delta} WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"ProductId\"={p.GiftProductId} AND \"Quarantined\">={qty}"); Check.That(changed == 1, "Quarantaine incohérente.", "INVENTORY_CONFLICT", 409);
            p.GiftStockState = disposition == "RELEASE" ? "RESTOCKED" : "DESTROYED"; Ledger(actor, p, "PROMOTION_GIFT_" + disposition, physical: delta, quarantine: -qty);
        }
        p.Version++; Event(actor, p, inspection ? "PROMOTION_GIFT_INSPECTED" : "PROMOTION_GIFT_RETURNED", key, qty, note: reason, hash: hash); Audit(actor, inspection ? "PROMOTION_GIFT_INSPECTED" : "PROMOTION_GIFT_RETURNED", id); await db.SaveChangesAsync(); return p;
    }
}
