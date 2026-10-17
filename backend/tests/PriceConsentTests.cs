using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Toushir.Wholesale;
using Xunit;

public sealed class PriceConsentTests
{
    private static readonly Actor Buyer = new("buyer-a", "workspace-a", "Acheteur", "MERCHANT", "APPROVED");
    private static readonly IDataProtectionProvider Protection = new EphemeralDataProtectionProvider();
    private static PricingService Service() => new(new WholesaleDb(new DbContextOptionsBuilder<WholesaleDb>().UseSqlite("Data Source=:memory:").Options, new ConfigurationBuilder().Build()), Protection);

    [Fact]
    public void ReviewedConsentIsBoundToTheActualActorShopWorkspaceAndCommercialTerms()
    {
        var service = Service();
        var proof = service.CreateConsent(Buyer, "shop-a", "terms-a");
        service.RequireConsent(Buyer, "shop-a", proof, "terms-a");
        foreach (var changed in new[] { Buyer with { Id = "buyer-b" }, Buyer with { WorkspaceId = "workspace-b" } })
            Assert.Equal("PRICE_CONFIRMATION_REQUIRED", Assert.Throws<ApiFailure>(() => service.RequireConsent(changed, "shop-a", proof, "terms-a")).Code);
        Assert.Throws<ApiFailure>(() => service.RequireConsent(Buyer, "shop-b", proof, "terms-a"));
        Assert.Throws<ApiFailure>(() => service.RequireConsent(Buyer, "shop-a", proof, "terms-b"));
    }

    [Fact]
    public void ExpiredEvenAuthenticallySignedConsentRequiresAnotherExplicitReview()
    {
        var service = Service();
        var protector = Protection.CreateProtector("Toushir.PriceConsent.v1");
        var proof = protector.Protect(JsonSerializer.Serialize(new { Buyer.WorkspaceId, ActorId = Buyer.Id,
            MerchantId = "shop-a", Fingerprint = "terms-a", ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) }));
        var error = Assert.Throws<ApiFailure>(() => service.RequireConsent(Buyer, "shop-a", proof, "terms-a"));
        Assert.Equal(409, error.Status); Assert.Equal("PRICE_CONFIRMATION_REQUIRED", error.Code);
    }

    [Theory][InlineData(null)][InlineData("")][InlineData("not-a-signed-consent")]
    public void MissingOrTamperedProofFailsClosed(string? proof)
        => Assert.Equal("PRICE_CONFIRMATION_REQUIRED", Assert.Throws<ApiFailure>(() => Service().RequireConsent(Buyer, "shop-a", proof, "terms-a")).Code);
}
