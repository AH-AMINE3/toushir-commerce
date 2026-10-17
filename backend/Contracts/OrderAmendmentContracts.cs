namespace Toushir.Wholesale;

public record ProposeOrderAmendmentInput(int Version, List<LineInput> Items, string Reason, DateTimeOffset ExpiresAt, string IdempotencyKey);
public record DecideOrderAmendmentInput(int Version, string IdempotencyKey, string? TermsHash = null, string? Reason = null);

public sealed record AmendmentLineTerms(string ProductId, string Name, string Brand, string ImageUrl,
    int Quantity, int CaseSize, int MinimumQuantity, decimal BaseUnitPrice, decimal UnitPrice, decimal LineTotal,
    string PricingSource, string RuleId, int RuleVersion, bool CampaignsAllowed,
    string CampaignId, int CampaignVersion, string CampaignTitle, string CampaignKind,
    string GiftProductId, string GiftProductName, int GiftQuantity);
public sealed record AmendmentTerms(IReadOnlyList<AmendmentLineTerms> Items, decimal Subtotal, decimal DeliveryFee,
    decimal Total, string Currency = "DZD", string DeliveryFeeBasis = "Frais du contrat initial conservés");
