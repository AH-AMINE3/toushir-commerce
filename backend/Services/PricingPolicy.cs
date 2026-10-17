namespace Toushir.Wholesale;

/// <summary>Published, immutable commercial terms. Publication and withdrawal bound eligibility
/// separately from the original effective dates; accepted order snapshots remain authoritative.</summary>
public sealed record PriceBand(string Id, string WorkspaceId, string ProductId, string Scope,
    string TargetId, int Version, long UnitPriceMinor, int MinimumQuantity, int? MaximumQuantity,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset? PublishedAt,
    DateTimeOffset? WithdrawnAt, bool AllowCampaigns, string Reference, string Status = "PUBLISHED");

public sealed record ApprovedOfferPrice(decimal? UnitPrice, bool Approved, bool Active = true);

public sealed record PricingDecision(string Source, string RuleId, int RuleVersion, long? UnitPriceMinor,
    bool AllowCampaigns, int MinimumQuantity, int? MaximumQuantity, DateTimeOffset? EndsAt, string Reference)
{
    public decimal? UnitPrice => UnitPriceMinor / 100m;
}

/// <summary>One deterministic policy for catalogue, baskets, manual quotes and direct ordering.
/// Quantity is always physical units. Missing prices never become zero-price sales.</summary>
public static class PricingPolicy
{
    private static int Priority(string scope) => scope switch { "MERCHANT" => 0, "SEGMENT" => 1, "STANDARD" => 2, _ => 99 };
    public static DateTimeOffset EffectiveStart(PriceBand band)
        => band.PublishedAt is { } published && published > band.StartsAt ? published : band.StartsAt;
    public static DateTimeOffset EffectiveEnd(PriceBand band)
        => band.WithdrawnAt is { } withdrawn && withdrawn < band.EndsAt ? withdrawn : band.EndsAt;

    public static void Validate(PriceBand band)
    {
        Check.That(!string.IsNullOrWhiteSpace(band.WorkspaceId) && !string.IsNullOrWhiteSpace(band.ProductId), "Le dépôt et le produit sont obligatoires.");
        Check.That(Priority(band.Scope) < 99 && (band.Scope == "STANDARD" ? band.TargetId.Length == 0 : !string.IsNullOrWhiteSpace(band.TargetId)), "Précisez un contrat de magasin, un segment ou le barème standard.");
        Check.That(band.UnitPriceMinor is > 0 and <= 10000000000, "Le prix doit être positif et exprimé en centimes DZD.");
        Check.That(band.MinimumQuantity is > 0 and <= 1000000 && (band.MaximumQuantity == null || band.MaximumQuantity >= band.MinimumQuantity && band.MaximumQuantity <= 1000000), "Vérifiez les bornes de quantité, exprimées en unités.");
        Check.That(band.EndsAt > band.StartsAt, "La fin doit suivre le début de validité.");
    }

    public static bool Overlaps(PriceBand left, PriceBand right)
        => left.WorkspaceId == right.WorkspaceId && left.ProductId == right.ProductId
            && left.Scope == right.Scope && left.TargetId == right.TargetId
            && left.MinimumQuantity <= (right.MaximumQuantity ?? int.MaxValue)
            && right.MinimumQuantity <= (left.MaximumQuantity ?? int.MaxValue)
            && EffectiveStart(left) < EffectiveEnd(right) && EffectiveStart(right) < EffectiveEnd(left)
            && EffectiveStart(left) < EffectiveEnd(left) && EffectiveStart(right) < EffectiveEnd(right);

    public static PricingDecision Resolve(IEnumerable<PriceBand> bands, string workspaceId,
        string? merchantId, string? segmentId, string productId, int quantity,
        DateTimeOffset at, ApprovedOfferPrice? offer)
    {
        Check.That(quantity is > 0 and <= 1000000, "La quantité doit être comprise entre 1 et 1 000 000 unités.");
        var unknown = new PricingDecision("ON_REQUEST", "", 0, null, false, 1, null, null, "Prix à confirmer par le grossiste");
        if (offer?.Active == false) return unknown;
        var eligible = bands.Where(b => b.WorkspaceId == workspaceId && b.ProductId == productId
            && b.Status == "PUBLISHED" && b.PublishedAt != null && EffectiveStart(b) <= at && at < EffectiveEnd(b)
            && b.MinimumQuantity <= quantity && (b.MaximumQuantity == null || quantity <= b.MaximumQuantity)
            && (b.Scope == "MERCHANT" && merchantId != null && b.TargetId == merchantId
                || b.Scope == "SEGMENT" && segmentId != null && b.TargetId == segmentId
                || b.Scope == "STANDARD" && b.TargetId.Length == 0)).ToList();
        if (eligible.Count > 0)
        {
            var priority = eligible.Min(b => Priority(b.Scope));
            var selected = eligible.Where(b => Priority(b.Scope) == priority).ToList();
            Check.That(selected.Count == 1, "Plusieurs tarifs de même priorité se chevauchent. Faites contrôler les règles avant de vendre.", "PRICING_CONFLICT", 409);
            var band = selected[0];
            Validate(band);
            return new(band.Scope, band.Id, band.Version, band.UnitPriceMinor, band.AllowCampaigns,
                band.MinimumQuantity, band.MaximumQuantity, EffectiveEnd(band), band.Reference);
        }
        if (offer is { Approved: true, UnitPrice: > 0 } && offer.UnitPrice <= 100000000m
            && decimal.Round(offer.UnitPrice.Value, 2) == offer.UnitPrice.Value)
            return new("OFFER", "", 0, checked((long)(offer.UnitPrice.Value * 100m)), true, 1, null, null, "Offre approuvée du dépôt");
        return unknown;
    }
}
