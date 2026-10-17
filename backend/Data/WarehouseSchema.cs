using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
namespace Toushir.Wholesale;

public static class WarehouseSchema
{
    public static void Configure(ModelBuilder m)
    {
        m.Entity<LotProductControl>().HasIndex(x => new { x.WorkspaceId, x.ProductId }).IsUnique();
        m.Entity<WarehouseLot>().HasIndex(x => new { x.WorkspaceId, x.SourceKey }).IsUnique();
        m.Entity<WarehouseLot>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<LotAllocation>().HasIndex(x => new { x.OrderId, x.AllocationKey, x.LotId }).IsUnique();
        m.Entity<LotAllocation>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<WarehouseOperation>().HasIndex(x => new { x.WorkspaceId, x.ActorId, x.IdempotencyKey }).IsUnique();
    }
    public static async Task Upgrade(WholesaleDb db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var tables = new[] { "LotProductControls", "WarehouseLots", "LotAllocations", "PickWaves", "WarehouseOperations" };
        foreach (Match match in Regex.Matches(db.Database.GenerateCreateScript(), @"CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX)\s+.*?;", RegexOptions.Singleline))
        {
            if (!tables.Any(t => match.Value.Contains("\"" + t + "\"", StringComparison.Ordinal)))
                continue;
            await db.Database.ExecuteSqlRawAsync(Regex.Replace(match.Value, @"^(CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX))\s+", "$1 IF NOT EXISTS "));
        }
        var at = DateTimeOffset.UtcNow.ToString("O");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommerceSchemaHistory\" (\"Version\",\"AppliedAt\") VALUES ({"20261017-warehouse-v1"},{at}) ON CONFLICT (\"Version\") DO NOTHING");
        await transaction.CommitAsync();
    }
}
