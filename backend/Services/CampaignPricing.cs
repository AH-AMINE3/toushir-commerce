namespace Toushir.Wholesale;

public sealed record CampaignPricePreview(string ProductId, decimal BaseUnitPrice, decimal AppliedUnitPrice,
    string CampaignId, int CampaignVersion, string CampaignTitle, string Kind, int MinimumQuantity,
    string GiftProductId, int GiftQuantity, string Issue = "", string GiftProductName = "");

/// <summary>The same selection and rounding for a non-binding basket preview and the committed
/// promotion snapshot. Stock and actual custody remain separate checks.</summary>
public static class CampaignPricing
{
    public static Campaign? Select(IEnumerable<Campaign> campaigns, string productId, int quantity)
        => campaigns.Where(x => x.ProductId == productId && quantity >= x.MinimumQuantity)
            .OrderByDescending(x => x.Kind == "DISCOUNT" ? x.DiscountPercent : -1m)
            .ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();

    public static decimal Applied(Campaign campaign, decimal basePrice, int quantity)
    {
        Check.Money(basePrice, "Prix de base");
        var applied = campaign.Kind == "DISCOUNT"
            ? decimal.Round(basePrice * (100m - campaign.DiscountPercent) / 100m, 2, MidpointRounding.AwayFromZero)
            : basePrice;
        Check.That(applied > 0 || quantity == 0, "La remise produit un prix nul. Utilisez une gratuité explicite.", "PROMOTION_PRICE_INVALID", 409);
        return applied;
    }
}
