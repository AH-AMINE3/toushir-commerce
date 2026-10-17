using Toushir.Wholesale;
using Xunit;

public sealed class PricingPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private static PriceBand Band(string scope = "MERCHANT", string target = "shop-a", long price = 12000,
        int minimum = 1, int? maximum = null) => new(Guid.NewGuid().ToString("N"), "workspace-a", "product-a", scope,
            target, 2, price, minimum, maximum, Now.AddDays(-1), Now.AddDays(10), Now.AddDays(-1), null, false, "Accord explicite");
    private static PricingDecision Resolve(IEnumerable<PriceBand> bands, int quantity = 1, DateTimeOffset? at = null,
        ApprovedOfferPrice? offer = null) => PricingPolicy.Resolve(bands, "workspace-a", "shop-a", "segment-a", "product-a", quantity, at ?? Now, offer);

    [Fact] public void ContractPriorityIsNotACheapestPriceCompetition()
    {
        var result = Resolve([Band(price: 15000), Band("SEGMENT", "segment-a", 11000), Band("STANDARD", "", 10000)]);
        Assert.Equal("MERCHANT", result.Source);
        Assert.Equal(150m, result.UnitPrice);
        Assert.False(result.AllowCampaigns);
    }

    [Theory][InlineData(11, "MERCHANT", 120)][InlineData(12, "SEGMENT", 110)]
    public void QuantityBandsUseInclusiveUnitBoundsAndFallBackOnlyWhenIneligible(int quantity, string source, decimal price)
    {
        var result = Resolve([Band(maximum: 11), Band("SEGMENT", "segment-a", 11000)], quantity);
        Assert.Equal(source, result.Source); Assert.Equal(price, result.UnitPrice);
    }

    [Fact] public void ApplicableStandardSchedulePrecedesApprovedOffer()
    {
        var result = Resolve([Band("STANDARD", "", 1999)], offer: new(10m, true));
        Assert.Equal("STANDARD", result.Source); Assert.Equal(19.99m, result.UnitPrice);
    }

    [Fact] public void ApprovedOfferIsTheLastKnownStandardFallback()
    {
        var result = Resolve([], offer: new(19.99m, true));
        Assert.Equal("OFFER", result.Source); Assert.Equal(1999, result.UnitPriceMinor); Assert.True(result.AllowCampaigns);
    }

    [Theory][InlineData(null, false)][InlineData(0d, true)][InlineData(10d, false)][InlineData(0.001d, true)]
    public void MissingZeroUnapprovedOrFractionalCentimeOfferDoesNotAuthorizeAZeroSale(double? price, bool approved)
    {
        var result = Resolve([], offer: new(price is null ? null : (decimal)price, approved));
        Assert.Null(result.UnitPrice); Assert.Equal("ON_REQUEST", result.Source);
    }

    [Fact] public void InactiveProductCannotBeEnabledByAPriceRule()
        => Assert.Null(Resolve([Band()], offer: new(10m, true, false)).UnitPrice);

    [Fact] public void OtherWorkspacesShopsProductsAndSegmentsCannotLeakPrices()
    {
        var rules = new[] { Band() with { WorkspaceId = "workspace-b" }, Band(target: "shop-b"),
            Band() with { ProductId = "product-b" }, Band("SEGMENT", "segment-b") };
        Assert.Null(Resolve(rules).UnitPrice);
    }

    [Theory][InlineData("DRAFT")][InlineData("REJECTED")][InlineData("RETIRED")]
    public void UnpublishedOrRetiredDefinitionsDoNotPriceTheBasket(string status)
        => Assert.Null(Resolve([Band() with { Status = status }]).UnitPrice);

    [Fact] public void NullPublicationCannotBeMadeActiveByStatusAlone()
        => Assert.Null(Resolve([Band() with { PublishedAt = null }]).UnitPrice);

    [Fact] public void ANewPublicationDoesNotApplyBeforeItsActualPublicationTime()
    {
        var rule = Band() with { PublishedAt = Now.AddHours(1) };
        Assert.Null(Resolve([rule]).UnitPrice);
        Assert.Equal(120m, Resolve([rule], at: Now.AddHours(1)).UnitPrice);
    }

    [Fact] public void FutureReplacementPreservesTheCurrentPriceUntilItsEffectiveInstant()
    {
        var boundary = Now.AddDays(1);
        var previous = Band(price: 12000) with { WithdrawnAt = boundary };
        var replacement = Band(price: 13000) with { StartsAt = boundary, PublishedAt = Now };
        Assert.False(PricingPolicy.Overlaps(previous, replacement));
        Assert.Equal(120m, Resolve([previous, replacement], at: boundary.AddTicks(-1)).UnitPrice);
        Assert.Equal(130m, Resolve([previous, replacement], at: boundary).UnitPrice);
    }

    [Fact] public void EndDateIsExclusiveAndAlgerianOffsetsPreserveTheSameInstant()
    {
        var rule = Band() with { EndsAt = Now.ToOffset(TimeSpan.FromHours(1)) };
        Assert.Equal(120m, Resolve([rule], at: Now.AddTicks(-1)).UnitPrice);
        Assert.Null(Resolve([rule], at: Now).UnitPrice);
    }

    [Fact] public void AmbiguousHighestPriorityRulesFailClosedInsteadOfSilentlyChoosingOne()
    {
        var error = Assert.Throws<ApiFailure>(() => Resolve([Band(price: 10000), Band(price: 15000)]));
        Assert.Equal("PRICING_CONFLICT", error.Code); Assert.Equal(409, error.Status);
    }

    [Fact] public void LowerPriorityOverlapDoesNotDisplaceOneValidMerchantContract()
        => Assert.Equal(120m, Resolve([Band(), Band("SEGMENT", "segment-a", 13000), Band("STANDARD", "", 14000)]).UnitPrice);

    [Fact] public void DisjointQuantityTiersAreValidButSharingAnInclusiveBoundaryConflicts()
    {
        var first = Band(maximum: 11);
        Assert.False(PricingPolicy.Overlaps(first, Band(minimum: 12)));
        Assert.True(PricingPolicy.Overlaps(first, Band(minimum: 11)));
    }

    [Fact] public void OverlapRequiresTheSameWorkspaceProductScopeAndTarget()
    {
        var rule = Band(); Assert.True(PricingPolicy.Overlaps(rule, Band()));
        foreach (var other in new[] { Band() with { WorkspaceId = "workspace-b" }, Band() with { ProductId = "product-b" },
            Band(target: "shop-b"), Band("SEGMENT", "shop-a") }) Assert.False(PricingPolicy.Overlaps(rule, other));
    }

    [Theory][InlineData(0, 1, null)][InlineData(-1, 1, null)][InlineData(100, 0, null)][InlineData(100, 10, 9)]
    public void InvalidMoneyOrQuantityBandsCannotBePublished(long minor, int min, int? max)
        => Assert.Throws<ApiFailure>(() => PricingPolicy.Validate(Band(price: minor, minimum: min, maximum: max)));

    [Fact] public void ACallerCannotEnableCampaignStackingWithoutTheChosenRuleAllowingIt()
    {
        Assert.False(Resolve([Band()]).AllowCampaigns);
        Assert.True(Resolve([Band() with { AllowCampaigns = true }]).AllowCampaigns);
    }
}
