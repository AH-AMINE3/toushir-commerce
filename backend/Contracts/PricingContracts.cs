namespace Toushir.Wholesale;

public record PriceSegmentInput(string Name);
public record AssignPriceSegmentInput(string? SegmentId, int Version, string Reason);
public record TariffRuleInput(string ProductId, string Scope, string? TargetId,
    int MinimumQuantity, int? MaximumQuantity, long UnitPriceMinor, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, bool AllowCampaigns, string Reference, string? Note, string IdempotencyKey,
    string? SupersedesId = null, int? SupersedesVersion = null);
public record ReviewTariffRuleInput(int Version, string Decision, string Reason);
public record RetireTariffRuleInput(int Version, string Reason);
public record SimulatePricingInput(string? MerchantId, List<LineInput> Items, DateTimeOffset? At = null);
