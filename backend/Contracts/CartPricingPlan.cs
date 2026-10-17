namespace Toushir.Wholesale;

public sealed record CartPricingItem(object Product, int Quantity, decimal? UnitPrice, decimal? LineTotal,
    int? Available, IReadOnlyList<string> Issues, PricingDecision Pricing, CampaignPricePreview? Promotion);
public sealed record CartPricingView(IReadOnlyList<CartPricingItem> Items, decimal? Subtotal, decimal? DeliveryFee,
    decimal? Total, bool NeedsQuote, bool CanOrder, string Currency, string? PricingToken, DateTimeOffset ResolvedAt);
public sealed record CartPricingPlan(CartPricingView View, IReadOnlyDictionary<string, Product> Products,
    IReadOnlyDictionary<string, Offer> Offers, QuotePricingPlan Prices,
    IReadOnlyDictionary<string, CampaignPricePreview> Promotions, string Fingerprint);
