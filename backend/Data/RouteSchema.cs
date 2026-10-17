using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public static class RouteSchema
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<DeliveryRoute>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<DeliveryRoute>().HasIndex(x => new { x.WorkspaceId, x.DriverId });
        model.Entity<RouteCustodyChallenge>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<RouteCustodyReceipt>().HasIndex(x => new { x.WorkspaceId, x.RouteId }).IsUnique();
    }
    public static async Task Upgrade(WholesaleDb db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await OperationsSchema.AddColumn(db,"DeliveryRoutes","CapacityUnits","INTEGER NULL","integer NULL");
        await OperationsSchema.AddColumn(db,"DeliveryAssignments","StopRank","INTEGER NOT NULL DEFAULT 0","integer NOT NULL DEFAULT 0");
        await OperationsSchema.AddColumn(db, "DeliveryAssignments", "RouteId", "TEXT NOT NULL DEFAULT ''", "text NOT NULL DEFAULT ''");
        await OperationsSchema.AddColumn(db, "CashReceipts", "RouteId", "TEXT NOT NULL DEFAULT ''", "text NOT NULL DEFAULT ''");
        await OperationsSchema.AddColumn(db, "ReceiptChallenges", "RouteId", "TEXT NOT NULL DEFAULT ''", "text NOT NULL DEFAULT ''");
        await OperationsSchema.AddColumn(db, "ReceiptChallenges", "RouteVersion", "INTEGER NOT NULL DEFAULT 0", "integer NOT NULL DEFAULT 0");
        var tables = new[] { "DeliveryRoutes", "RouteCustodyChallenges", "RouteCustodyReceipts" };
        foreach (Match match in Regex.Matches(db.Database.GenerateCreateScript(), @"CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX)\s+.*?;", RegexOptions.Singleline))
        {
            if (!tables.Any(table => match.Value.Contains("\"" + table + "\"", StringComparison.Ordinal))) continue;
            var command = Regex.Replace(match.Value, @"^(CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX))\s+", "$1 IF NOT EXISTS ");
            await db.Database.ExecuteSqlRawAsync(command);
        }
        var at = DateTimeOffset.UtcNow.ToString("O");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommerceSchemaHistory\" (\"Version\",\"AppliedAt\") VALUES ({"20261017-routes-v1"},{at}) ON CONFLICT (\"Version\") DO NOTHING");
        await transaction.CommitAsync();
    }
}
