using Toushir.Wholesale;
using Xunit;

public sealed class DistributionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "toushir-distribution-" + Guid.NewGuid().ToString("N"));

    public DistributionTests() => Directory.CreateDirectory(Path.Combine(root, "downloads"));
    public void Dispose() => Directory.Delete(root, true);
    private void File(string name, string text) => System.IO.File.WriteAllText(Path.Combine(root, "downloads", name), text);

    [Fact]
    public void MissingManifestOrArtifactCannotAdvertiseADownload()
    {
        Assert.False(DistributionEndpoints.Snapshot(root, "https://commerce.example").Merchant.Available);
        File("manifest.json", "{\"merchant\":{\"apiOrigin\":\"https://commerce.example\"}}");
        Assert.False(DistributionEndpoints.Snapshot(root, "https://commerce.example").Merchant.Available);
    }

    [Fact]
    public void EachAppRequiresItsOwnMatchingHttpsManifestAndFixedArtifact()
    {
        File("manifest.json", "{\"merchant\":{\"apiOrigin\":\"https://commerce.example\"},\"driver\":{\"apiOrigin\":\"https://other.example\"}}");
        File("toushir-merchant.apk", "test artifact"); File("toushir-driver.apk", "test artifact");
        var result = DistributionEndpoints.Snapshot(root, "https://commerce.example/");
        Assert.True(result.Merchant.Available);
        Assert.Equal("https://commerce.example/downloads/toushir-merchant.apk", result.Merchant.Url);
        Assert.Equal("https://commerce.example", result.Merchant.ApiOrigin);
        Assert.False(result.Driver.Available); Assert.Null(result.Driver.Url);
    }

    [Theory]
    [InlineData("http://10.0.2.2:5860")]
    [InlineData("http://127.0.0.1:5860")]
    [InlineData("https://commerce.example/other")]
    [InlineData("https://commerce.example?api=other")]
    [InlineData("https://user@commerce.example")]
    public void LocalOrNonOriginMetadataCannotAdvertisePublicApks(string origin)
    {
        File("manifest.json", System.Text.Json.JsonSerializer.Serialize(new { merchant = new { apiOrigin = origin } }));
        File("toushir-merchant.apk", "test artifact");
        Assert.False(DistributionEndpoints.Snapshot(root, "https://commerce.example").Merchant.Available);
        Assert.False(DistributionEndpoints.Snapshot(root, origin).Merchant.Available);
    }

    [Fact]
    public void InvalidJsonFailsClosedWithoutExposingStorageDetails()
    {
        File("manifest.json", "not valid JSON"); File("toushir-merchant.apk", "test artifact");
        var result = DistributionEndpoints.Snapshot(root, "https://commerce.example");
        Assert.False(result.Merchant.Available); Assert.Null(result.Merchant.ApiOrigin); Assert.False(result.Driver.Available);
    }
}
