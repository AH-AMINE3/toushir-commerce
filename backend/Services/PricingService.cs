using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace Toushir.Wholesale;

public sealed class PricingBook(string workspaceId, string? merchantId, string? segmentId,
    DateTimeOffset at, IReadOnlyList<PriceBand> bands)
{
    private readonly Dictionary<string, List<PriceBand>> byProduct = bands.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.ToList());
    public DateTimeOffset At => at;
    public PricingDecision Resolve(string productId, int quantity, Offer? offer)
        => PricingPolicy.Resolve(byProduct.GetValueOrDefault(productId) ?? [], workspaceId, merchantId,
            segmentId, productId, quantity, at, offer == null ? null : new(offer.Price, offer.OfferApproved, offer.IsActive));
    public PriceBand? Terms(string ruleId) => bands.FirstOrDefault(x => x.Id == ruleId);
}

/// <summary>Workspace-scoped price definitions and independent publication. Actual order terms
/// are snapshotted separately; changing a rule never recalculates an existing contract.</summary>
public sealed partial class PricingService(WholesaleDb db, IDataProtectionProvider protection)
{
    private static void Version(int actual, int expected) => Check.That(actual == expected,
        "Le tarif a changé. Actualisez avant de continuer.", "VERSION_CONFLICT", 409);
    public static PriceBand Band(TariffRule rule) => new(rule.Id, rule.WorkspaceId, rule.ProductId, rule.Scope,
        rule.TargetId, rule.Version, rule.UnitPriceMinor, rule.MinimumQuantity, rule.MaximumQuantity,
        rule.StartsAt, rule.EndsAt, rule.PublishedAt, rule.WithdrawnAt, rule.AllowCampaigns, rule.Reference, rule.Status);

    private void Event(Actor actor, string id, string kind, string details)
    {
        var now = DateTimeOffset.UtcNow;
        db.PricingEvents.Add(new() { WorkspaceId = actor.WorkspaceId, AggregateId = id, Kind = kind,
            ActorId = actor.Id, Details = details, At = now, UtcTicks = now.UtcTicks });
        db.Audit.Add(new() { WorkspaceId = actor.WorkspaceId, ActorId = actor.Id, Actor = actor.Name,
            EntityId = id, Action = kind, Details = details });
        db.EngagementNotices.Add(new() { WorkspaceId = actor.WorkspaceId, Kind = kind, AggregateId = id });
    }

    private async Task<Account> Merchant(string workspaceId, string id)
    {
        var merchant = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id && x.Role == "MERCHANT");
        Check.That(merchant != null, "Magasin introuvable.", "NOT_FOUND", 404);
        return merchant;
    }

    private async Task<TariffRule> Rule(Actor actor, string id)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        var rule = await db.TariffRules.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.Id == id);
        Check.That(rule != null, "Règle tarifaire introuvable.", "NOT_FOUND", 404);
        return rule;
    }

    private static object View(TariffRule rule, string? productName = null)
    {
        var now = DateTimeOffset.UtcNow;
        var band = Band(rule);
        var state = rule.Status != "PUBLISHED" ? rule.Status
            : rule.WithdrawnAt <= now ? "RETIRED" : rule.EndsAt <= now ? "EXPIRED"
            : PricingPolicy.EffectiveStart(band) > now ? "SCHEDULED" : "ACTIVE";
        return new { rule.Id, rule.ProductId, productName, rule.Scope, rule.TargetId, rule.MinimumQuantity,
            rule.MaximumQuantity, rule.UnitPriceMinor, rule.StartsAt, rule.EndsAt, rule.AllowCampaigns,
            rule.Reference, rule.Note, rule.Status, state, rule.Version, rule.CreatedBy, rule.CreatedAt,
            rule.ReviewedBy, rule.ReviewReason, rule.ReviewedAt, rule.PublishedAt, rule.WithdrawnAt,
            rule.WithdrawnBy, rule.WithdrawalReason, rule.SupersedesId, rule.SupersedesVersion,
            currency = "DZD", monetaryUnit = "CENTIME", quantityUnit = "UNIT" };
    }

    public async Task<object> Overview(Actor actor)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        var rules = await db.TariffRules.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).ToListAsync();
        var ids = rules.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        return new
        {
            rules = rules.OrderByDescending(x => x.CreatedAt).Select(x => View(x, products.GetValueOrDefault(x.ProductId))),
            segments = await db.PriceSegments.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).OrderBy(x => x.Name).ToListAsync(),
            assignments = await db.MerchantPriceSegments.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).ToListAsync(),
            merchants = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.Role == "MERCHANT")
                .OrderBy(x => x.ShopName).Select(x => new { x.Id, x.Name, x.ShopName, x.ApprovalStatus }).ToListAsync(),
            events = await db.PricingEvents.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId)
                .OrderByDescending(x => x.UtcTicks).ThenBy(x => x.Id).Take(200).ToListAsync(),
            policy = "Premier tarif valide : contrat du magasin, segment commercial, barème standard, offre approuvée du dépôt. Aucun prix manquant ne vaut zéro.",
            campaignPolicy = "Une campagne ne se cumule avec un tarif que si cette règle l’autorise. Les contrats déjà chiffrés conservent leur historique.",
            currency = "DZD", monetaryUnit = "CENTIME", quantityUnit = "UNIT"
        };
    }

    public async Task<object> CreateSegment(Actor actor, PriceSegmentInput input)
    {
        actor.Admin();
        var name = Check.Text(input.Name, 100, "Nom du segment");
        var normalized = name.ToUpperInvariant();
        Check.That(!await db.PriceSegments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.NormalizedName == normalized), "Ce segment existe déjà.", "SEGMENT_EXISTS", 409);
        Check.That(await db.PriceSegments.CountAsync(x => x.WorkspaceId == actor.WorkspaceId) < 200, "La limite de segments est atteinte.", "PRICING_QUOTA", 409);
        var segment = new PriceSegment { WorkspaceId = actor.WorkspaceId, Name = name, NormalizedName = normalized, CreatedBy = actor.Id };
        db.PriceSegments.Add(segment);
        Event(actor, segment.Id, "PRICE_SEGMENT_CREATED", name);
        await db.SaveChangesAsync();
        return segment;
    }

    public async Task<object> AssignSegment(Actor actor, string merchantId, AssignPriceSegmentInput input)
    {
        actor.Admin();
        await Merchant(actor.WorkspaceId, merchantId);
        var segmentId = Check.Text(input.SegmentId, 100, "Segment", false);
        if (segmentId.Length > 0)
            Check.That(await db.PriceSegments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.Id == segmentId && x.IsActive), "Segment actif introuvable.", "NOT_FOUND", 404);
        var reason = Check.Text(input.Reason, 1000, "Motif de l’affectation");
        var assignment = await db.MerchantPriceSegments.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == merchantId);
        Version(assignment?.Version ?? 0, input.Version);
        var previous = assignment?.SegmentId ?? "";
        if (assignment == null)
        {
            assignment = new() { WorkspaceId = actor.WorkspaceId, MerchantId = merchantId };
            db.MerchantPriceSegments.Add(assignment);
        }
        else assignment.Version++;
        assignment.SegmentId = segmentId; assignment.UpdatedBy = actor.Id; assignment.UpdatedAt = DateTimeOffset.UtcNow;
        Event(actor, merchantId, "MERCHANT_PRICE_SEGMENT_CHANGED", JsonSerializer.Serialize(new { previous, segmentId, reason }));
        await db.SaveChangesAsync();
        return assignment;
    }

    private async Task ValidateTarget(Actor actor, string scope, string targetId)
    {
        if (scope == "MERCHANT") await Merchant(actor.WorkspaceId, targetId);
        if (scope == "SEGMENT") Check.That(await db.PriceSegments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.Id == targetId && x.IsActive), "Segment actif introuvable.", "NOT_FOUND", 404);
    }

    public async Task<object> CreateRule(Actor actor, TariffRuleInput input)
    {
        actor.Admin();
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d’enregistrement");
        var rule = new TariffRule { WorkspaceId = actor.WorkspaceId,
            ProductId = Check.Text(input.ProductId, 100, "Produit"), Scope = Check.Text(input.Scope, 20, "Portée").ToUpperInvariant(),
            TargetId = Check.Text(input.TargetId, 100, "Cible", false), MinimumQuantity = input.MinimumQuantity,
            MaximumQuantity = input.MaximumQuantity, UnitPriceMinor = input.UnitPriceMinor, StartsAt = input.StartsAt.ToUniversalTime(),
            EndsAt = input.EndsAt.ToUniversalTime(), AllowCampaigns = input.AllowCampaigns,
            Reference = Check.Text(input.Reference, 180, "Référence de l’accord"), Note = Check.Text(input.Note, 1000, "Note", false),
            SupersedesId = Check.Text(input.SupersedesId, 100, "Tarif remplacé", false), SupersedesVersion = input.SupersedesVersion,
            IdempotencyKey = key, CreatedBy = actor.Id };
        PricingPolicy.Validate(Band(rule));
        Check.That(rule.SupersedesId.Length > 0 || rule.SupersedesVersion == null, "Une version remplacée exige un tarif d’origine.");
        var hash = Tokens.Hash(JsonSerializer.Serialize(new { actor.Id, rule.ProductId, rule.Scope, rule.TargetId, rule.MinimumQuantity,
            rule.MaximumQuantity, rule.UnitPriceMinor, rule.StartsAt, rule.EndsAt, rule.AllowCampaigns, rule.Reference,
            rule.Note, rule.SupersedesId, rule.SupersedesVersion }));
        var replay = await db.TariffRules.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.IdempotencyKey == key);
        if (replay != null) { Check.That(replay.RequestHash == hash, "Cette clé désigne une autre règle.", "IDEMPOTENCY_CONFLICT", 409); return View(replay); }
        Check.That(rule.EndsAt > DateTimeOffset.UtcNow.AddMinutes(1) && rule.StartsAt >= DateTimeOffset.UtcNow.AddYears(-1)
            && rule.EndsAt <= DateTimeOffset.UtcNow.AddYears(5), "Vérifiez la période de validité du tarif.");
        Check.That(await db.Products.AnyAsync(x => x.Id == rule.ProductId && x.IsCatalogActive), "Produit actif introuvable.", "NOT_FOUND", 404);
        await ValidateTarget(actor, rule.Scope, rule.TargetId);
        if (rule.SupersedesId.Length > 0)
        {
            var old = await Rule(actor, rule.SupersedesId);
            Version(old.Version, rule.SupersedesVersion ?? -1);
            ValidateReplacement(old, rule);
        }
        var demo = await db.Workspaces.AnyAsync(x => x.Id == actor.WorkspaceId && x.IsDemo);
        Check.That(await db.TariffRules.CountAsync(x => x.WorkspaceId == actor.WorkspaceId) < (demo ? 100 : 2000), "La limite de règles conservées est atteinte.", "PRICING_QUOTA", 409);
        rule.RequestHash = hash;
        db.TariffRules.Add(rule);
        Event(actor, rule.Id, "TARIFF_PROPOSED", rule.Reference);
        await db.SaveChangesAsync();
        return View(rule);
    }

    private static void ValidateReplacement(TariffRule old, TariffRule next)
    {
        Check.That(old.Status == "PUBLISHED" && old.WithdrawnAt == null && old.ProductId == next.ProductId
            && old.Scope == next.Scope && old.TargetId == next.TargetId && old.MinimumQuantity == next.MinimumQuantity
            && old.MaximumQuantity == next.MaximumQuantity,
            "Une révision doit remplacer un tarif publié non retiré, pour le même produit, la même cible et la même tranche de quantité.", "TARIFF_REPLACEMENT_CONFLICT", 409);
    }

    public async Task<object> ReviewRule(Actor actor, string id, ReviewTariffRuleInput input)
    {
        var rule = await Rule(actor, id);
        Version(rule.Version, input.Version);
        Check.That(rule.Status == "DRAFT", "Cette proposition a déjà été examinée.", "INVALID_TRANSITION", 409);
        Check.That(rule.CreatedBy != actor.Id, "Une autre personne habilitée doit examiner ce tarif.", "INDEPENDENT_APPROVAL_REQUIRED", 409);
        var decision = input.Decision?.Trim().ToUpperInvariant();
        Check.That(decision is "PUBLISH" or "REJECT", "Choisissez Publier ou Rejeter.");
        var reason = Check.Text(input.Reason, 1000, "Motif de la décision");
        var now = DateTimeOffset.UtcNow;
        if (decision == "PUBLISH")
        {
            Check.That(rule.EndsAt > now, "Cette règle a expiré. Proposez une nouvelle période.", "TARIFF_EXPIRED", 409);
            Check.That(await db.Products.AnyAsync(x => x.Id == rule.ProductId && x.IsCatalogActive), "Le produit n’est plus actif.", "NOT_FOUND", 404);
            await ValidateTarget(actor, rule.Scope, rule.TargetId);
            var candidate = Band(rule) with { PublishedAt = now, Status = "PUBLISHED" };
            var others = await db.TariffRules.Where(x => x.WorkspaceId == actor.WorkspaceId && x.ProductId == rule.ProductId && x.Status == "PUBLISHED").ToListAsync();
            Check.That(!others.Any(x => x.Id != rule.SupersedesId && PricingPolicy.Overlaps(Band(x), candidate)),
                "Une règle publiée de même portée chevauche cette période et cette tranche de quantité.", "TARIFF_OVERLAP", 409);
            if (rule.SupersedesId.Length > 0)
            {
                var previous = others.FirstOrDefault(x => x.Id == rule.SupersedesId);
                Check.That(previous != null, "Le tarif à remplacer n’est plus publié.", "TARIFF_REPLACEMENT_CONFLICT", 409);
                Version(previous.Version, rule.SupersedesVersion ?? -1);
                ValidateReplacement(previous, rule);
                previous.WithdrawnAt = PricingPolicy.EffectiveStart(candidate);
                previous.WithdrawnBy = actor.Id; previous.WithdrawalReason = "Remplacé par " + rule.Id + " · " + reason; previous.Version++;
                Event(actor, previous.Id, "TARIFF_REPLACEMENT_SCHEDULED", JsonSerializer.Serialize(new { replacement = rule.Id, previous.WithdrawnAt, reason }));
            }
            rule.PublishedAt = now; rule.Status = "PUBLISHED";
        }
        else rule.Status = "REJECTED";
        rule.ReviewedAt = now; rule.ReviewedBy = actor.Id; rule.ReviewReason = reason; rule.Version++;
        Event(actor, rule.Id, "TARIFF_" + rule.Status, reason);
        await db.SaveChangesAsync();
        return View(rule);
    }

    public async Task<object> RetireRule(Actor actor, string id, RetireTariffRuleInput input)
    {
        var rule = await Rule(actor, id);
        Version(rule.Version, input.Version);
        var now = DateTimeOffset.UtcNow;
        Check.That(rule.Status == "PUBLISHED" && (rule.WithdrawnAt == null || rule.WithdrawnAt > now) && rule.EndsAt > now,
            "Seul un tarif publié et non retiré peut être retiré.", "INVALID_TRANSITION", 409);
        Check.That(rule.CreatedBy != actor.Id, "Une autre personne habilitée doit retirer un tarif publié.", "INDEPENDENT_APPROVAL_REQUIRED", 409);
        rule.WithdrawalReason = Check.Text(input.Reason, 1000, "Motif du retrait");
        var previousWithdrawalAt = rule.WithdrawnAt;
        rule.WithdrawnAt = now; rule.WithdrawnBy = actor.Id; rule.Version++;
        Event(actor, id, "TARIFF_WITHDRAWN", JsonSerializer.Serialize(new { previousWithdrawalAt, rule.WithdrawnAt, reason = rule.WithdrawalReason }));
        await db.SaveChangesAsync();
        return View(rule);
    }

    public async Task<PricingBook> LoadBook(string workspaceId, string? merchantId, DateTimeOffset? at = null)
    {
        string? segmentId = null;
        if (merchantId != null)
        {
            await Merchant(workspaceId, merchantId);
            var assignment = await db.MerchantPriceSegments.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.MerchantId == merchantId);
            if (assignment?.SegmentId is { Length: > 0 } value && await db.PriceSegments.AnyAsync(x => x.WorkspaceId == workspaceId && x.Id == value && x.IsActive)) segmentId = value;
        }
        var rules = await db.TariffRules.AsNoTracking().Where(x => x.WorkspaceId == workspaceId && x.Status == "PUBLISHED"
            && (x.Scope == "STANDARD" || x.Scope == "MERCHANT" && x.TargetId == merchantId || x.Scope == "SEGMENT" && x.TargetId == segmentId)).ToListAsync();
        return new(workspaceId, merchantId, segmentId, at?.ToUniversalTime() ?? DateTimeOffset.UtcNow, rules.Select(Band).ToList());
    }

}
