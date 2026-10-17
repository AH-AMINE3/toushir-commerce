using Microsoft.EntityFrameworkCore;
using Toushir.Wholesale;
using Xunit;

public sealed class ClaimConcurrencyTests
{
    [Fact]
    public async Task IndependentContextsCannotInspectTheSameReturnedUnitTwice()
    {
        var directory = Path.Combine(Path.GetTempPath(), "toushir-claim-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var connection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "claims.db") }.ConnectionString;
        try
        {
            await using var first = new WholesaleDesignTimeFactory().CreateDbContext([]);
            await using var second = new WholesaleDesignTimeFactory().CreateDbContext([]);
            first.Database.SetConnectionString(connection); second.Database.SetConnectionString(connection);
            await first.Database.EnsureCreatedAsync();
            first.Claims.Add(new OrderClaim { Id = "claim", WorkspaceId = "isolated", Status = "ACCEPTED", Quantity = 1, ReceivedQuantity = 1 });
            await first.SaveChangesAsync();
            var stale = await second.Claims.SingleAsync();
            first.Claims.Local.Single().InspectedQuantity = 1;
            await first.SaveChangesAsync();
            stale.InspectedQuantity = 1;
            await Assert.ThrowsAsync<Volo.Abp.Data.AbpDbConcurrencyException>(() => second.SaveChangesAsync());
            await first.Entry(first.Claims.Local.Single()).ReloadAsync();
            Assert.Equal(1, first.Claims.Local.Single().InspectedQuantity);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }
}
