using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed record PricingLinePlan(string ProductId, int Quantity, PricingDecision Decision,
    decimal BaseUnitPrice, string Source, bool CampaignsAllowed, string OverrideReason, string TermsJson);
public sealed record QuotePricingPlan(List<QuoteLineInput> Items, IReadOnlyDictionary<string, PricingLinePlan> Lines,
    IReadOnlySet<string> CampaignBlockedProductIds, DateTimeOffset At);
public sealed record CheckoutLineTerms(string ProductId, int Quantity, int CaseSize, int MinimumQuantity,
    string PricingSource, string RuleId, int RuleVersion, long BaseUnitPriceMinor, long AppliedUnitPriceMinor,
    bool CampaignsAllowed, string CampaignId, int CampaignVersion, string CampaignKind, string GiftProductId, int GiftQuantity);

public sealed partial class PricingService
{
    private sealed record PriceConsent(string WorkspaceId, string ActorId, string MerchantId, string Fingerprint, DateTimeOffset ExpiresAt);
    private IDataProtector ConsentProtector => protection.CreateProtector("Toushir.PriceConsent.v1");
    public static long Minor(decimal value) { Check.Money(value, "Montant"); return checked((long)(value * 100m)); }
    public static string Fingerprint(IReadOnlyList<CheckoutLineTerms> items, long deliveryFeeMinor)
        => Tokens.Hash(JsonSerializer.Serialize(new { currency = "DZD", deliveryFeeMinor,
            items = items.OrderBy(x => x.ProductId, StringComparer.Ordinal) }));

    public string CreateConsent(Actor actor, string merchantId, string fingerprint)
        => ConsentProtector.Protect(JsonSerializer.Serialize(new PriceConsent(actor.WorkspaceId, actor.Id,
            merchantId, fingerprint, DateTimeOffset.UtcNow.AddMinutes(5))));

    public void RequireConsent(Actor actor, string merchantId, string? token, string fingerprint)
    {
        const string message = "Actualisez votre panier et vérifiez les prix avant de confirmer la commande.";
        Check.That(token is { Length: > 0 and <= 4096 }, message, "PRICE_CONFIRMATION_REQUIRED", 409);
        PriceConsent? consent;
        try { consent = JsonSerializer.Deserialize<PriceConsent>(ConsentProtector.Unprotect(token)); }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException or FormatException)
        { throw new ApiFailure(409, "PRICE_CONFIRMATION_REQUIRED", message); }
        Check.That(consent != null && consent.WorkspaceId == actor.WorkspaceId && consent.ActorId == actor.Id
            && consent.MerchantId == merchantId && consent.Fingerprint == fingerprint && consent.ExpiresAt > DateTimeOffset.UtcNow,
            message, "PRICE_CONFIRMATION_REQUIRED", 409);
    }

    public async Task<QuotePricingPlan> PlanQuote(Actor actor, PurchaseOrder order, List<QuoteLineInput> requested,
        bool useTariffPrices, string? overrideReason)
    {
        actor.Admin();
        Check.That(order.WorkspaceId == actor.WorkspaceId, "Commande introuvable.", "NOT_FOUND", 404);
        Check.That(requested is { Count: > 0 and <= 200 } && requested.All(x => x != null)
            && requested.Count == order.Items.Count && requested.Select(x => x.ProductId).Distinct().Count() == requested.Count,
            "Le devis doit préciser chaque ligne une seule fois.");
        var book = await LoadBook(actor.WorkspaceId, order.AccountId);
        var ids = requested.Select(x => x.ProductId).ToList();
        var offers = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId);
        var lines = new Dictionary<string, PricingLinePlan>();
        var result = new List<QuoteLineInput>();
        var blocked = new HashSet<string>();
        var reason = Check.Text(overrideReason, 1000, "Motif de l’adaptation tarifaire", false);
        foreach (var input in requested)
        {
            var original = order.Items.FirstOrDefault(x => x.ProductId == input.ProductId);
            Check.That(original != null && input.Quantity >= 0 && input.Quantity <= original.RequestedQuantity,
                "Chaque ligne du devis doit correspondre à une quantité demandée.");
            if (input.Quantity == 0)
            {
                var unavailable = new PricingDecision("UNAVAILABLE", "", 0, null, false, 0, null, null, "Aucune unité proposée");
                lines[input.ProductId] = new(input.ProductId, 0, unavailable, 0, "UNAVAILABLE", false, "", "{}");
                result.Add(input with { UnitPrice = 0 }); blocked.Add(input.ProductId); continue;
            }
            var decision = book.Resolve(input.ProductId, input.Quantity, offers.GetValueOrDefault(input.ProductId));
            if (useTariffPrices) Check.That(decision.UnitPrice != null, "Un prix reste à confirmer. Saisissez un devis manuel explicite.", "PRICE_ON_REQUEST", 409);
            var actual = useTariffPrices ? decision.UnitPrice!.Value : input.UnitPrice;
            Check.Money(actual, "Prix de base");
            Check.That(actual > 0, "Un prix positif est requis pour les unités confirmées.");
            var manual = decision.UnitPrice != actual;
            var custom = decision.RuleId.Length > 0;
            if (manual && custom) Check.That(reason.Length >= 8,
                "Expliquez l’écart au tarif publié avant de proposer ce devis (8 caractères minimum).", "PRICE_OVERRIDE_REASON_REQUIRED", 409);
            var campaignsAllowed = !custom || decision.AllowCampaigns;
            if (!campaignsAllowed) blocked.Add(input.ProductId);
            var source = manual ? "MANUAL_QUOTE" : decision.Source;
            var terms = book.Terms(decision.RuleId);
            lines[input.ProductId] = new(input.ProductId, input.Quantity, decision, actual, source,
                campaignsAllowed, manual ? reason : "", JsonSerializer.Serialize(new { decision, terms }));
            result.Add(input with { UnitPrice = actual });
        }
        return new(result, lines, blocked, book.At);
    }

    public void Capture(Actor actor, PurchaseOrder order, QuotePricingPlan plan)
    {
        actor.RequireRoles("ADMIN", "MERCHANT");
        Check.That(order.WorkspaceId == actor.WorkspaceId && (actor.Role != "MERCHANT" || order.AccountId == actor.ResourceOwnerId), "Commande introuvable.", "NOT_FOUND", 404);
        foreach (var item in order.Items)
        {
            Check.That(plan.Lines.TryGetValue(item.ProductId, out var line), "La provenance tarifaire d’une ligne manque.", "PRICING_CONFLICT", 409);
            db.OrderPricingSnapshots.Add(new() { WorkspaceId = actor.WorkspaceId, OrderId = order.Id,
                QuoteVersion = order.QuoteVersion, MerchantId = order.AccountId, ActorId = actor.Id,
                ProductId = item.ProductId, Quantity = item.Quantity, Source = line.Source,
                ResolutionSource = line.Decision.Source, RuleId = line.Decision.RuleId,
                RuleVersion = line.Decision.RuleVersion, ExpectedUnitPriceMinor = line.Decision.UnitPriceMinor,
                BaseUnitPriceMinor = Minor(line.BaseUnitPrice), AppliedUnitPriceMinor = Minor(item.UnitPrice ?? 0),
                CampaignsAllowed = line.CampaignsAllowed, OverrideReason = line.OverrideReason,
                TermsJson = line.TermsJson, ResolvedAt = plan.At });
        }
    }

    private static object SnapshotView(Actor actor, OrderPricingSnapshot item)
    {
        if (actor.Role is "ADMIN" or "FINANCE") return new { item.Id, item.QuoteVersion, item.ProductId,
            item.Quantity, item.MerchantId, item.ActorId, item.Source, item.ResolutionSource, item.RuleId,
            item.RuleVersion, item.ExpectedUnitPriceMinor, item.BaseUnitPriceMinor, item.AppliedUnitPriceMinor,
            item.CampaignsAllowed, item.OverrideReason, item.TermsJson, item.ResolvedAt };
        return new { item.Id, item.QuoteVersion, item.ProductId, item.Quantity, item.Source,
            item.BaseUnitPriceMinor, item.AppliedUnitPriceMinor, item.CampaignsAllowed, item.ResolvedAt };
    }

    public async Task<object> CurrentPricing(Actor actor, PurchaseOrder order)
    {
        actor.RequireRoles("ADMIN", "FINANCE", "MERCHANT", "WAREHOUSE", "DISPATCHER", "DRIVER");
        Check.That(await db.Orders.AsNoTracking().AnyAsync(x => x.Id == order.Id && x.WorkspaceId == actor.WorkspaceId
            && (actor.Role != "MERCHANT" || x.AccountId == actor.ResourceOwnerId)), "Commande introuvable.", "NOT_FOUND", 404);
        if (actor.Role == "DRIVER") Check.That(await db.DeliveryAssignments.AsNoTracking().AnyAsync(x =>
            x.WorkspaceId == actor.WorkspaceId && x.OrderId == order.Id && x.DriverId == actor.Id), "Commande introuvable.", "NOT_FOUND", 404);
        var items = await db.OrderPricingSnapshots.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId
            && x.OrderId == order.Id && x.QuoteVersion == order.QuoteVersion).ToListAsync();
        return new { quoteVersion = order.QuoteVersion, recorded = items.Count > 0,
            items = items.Select(x => SnapshotView(actor, x)), currency = "DZD", monetaryUnit = "CENTIME",
            notice = items.Count > 0 ? "Conditions figées lors du chiffrage. Les nouveaux tarifs ne recalculent pas cette version."
                : "Aucune provenance tarifaire structurée pour cette version. Consultez le dossier et son historique d’origine." };
    }

    public async Task<object> History(Actor actor, string orderId, int page, int pageSize)
    {
        actor.RequireRoles("ADMIN", "FINANCE", "MERCHANT");
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId && x.WorkspaceId == actor.WorkspaceId);
        Check.That(order != null && (actor.Role != "MERCHANT" || order.AccountId == actor.ResourceOwnerId), "Commande introuvable.", "NOT_FOUND", 404);
        var query = db.OrderPricingSnapshots.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == orderId);
        var size = Math.Clamp(pageSize, 1, 100); var current = Math.Clamp(page, 1, 100000);
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.QuoteVersion).ThenBy(x => x.ProductId)
            .Skip((current - 1) * size).Take(size).ToListAsync();
        return new { items = items.Select(x => SnapshotView(actor, x)), total = count, page = current,
            pageSize = size, totalPages = (int)Math.Ceiling(count / (double)size), currency = "DZD", monetaryUnit = "CENTIME" };
    }
}
