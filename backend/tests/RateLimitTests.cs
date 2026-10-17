using Microsoft.AspNetCore.Http;
using Toushir.Wholesale;
using Xunit;

public sealed class RateLimitTests
{
    [Theory]
    [InlineData("/api/health", true)]
    [InlineData("/api/orders", true)]
    [InlineData("/connect/authorize", true)]
    [InlineData("/auth/login", true)]
    [InlineData("/hubs/commerce", true)]
    [InlineData("/.well-known/openid-configuration", true)]
    [InlineData("/catalog/images/product.jpg", false)]
    [InlineData("/assets/brand.svg", false)]
    [InlineData("/mobile/driver", false)]
    [InlineData("/downloads/toushir-driver.apk", false)]
    [InlineData("/apiary/font.woff2", false)]
    public void PublicAssetsHaveAnIndependentBudgetFromCommerceAndIdentity(string path, bool expected) =>
        Assert.Equal(expected, CommerceRateLimits.IsProtectedPath(new PathString(path)));
}
