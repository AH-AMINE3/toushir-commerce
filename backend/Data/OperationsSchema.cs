using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Toushir.Wholesale;

/// <summary>Additive, versioned upgrade for pre-existing wholesale databases; no data is reseeded or removed.</summary>
public static class OperationsSchema
{
    public static async Task Upgrade(WholesaleDb db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await AddColumn(db,"Sessions","OwnerCapability","INTEGER NOT NULL DEFAULT 0","boolean NOT NULL DEFAULT FALSE");
        if (db.Database.IsSqlite())
        {
            await AddSqliteColumn(db, "Orders", "Version", "INTEGER NOT NULL DEFAULT 1");
            await AddSqliteColumn(db, "OrderLine", "PreparedQuantity", "INTEGER NOT NULL DEFAULT 0");
            // The existing collection convention has used both names in development snapshots.
            await AddSqliteColumn(db, "OrderLines", "PreparedQuantity", "INTEGER NOT NULL DEFAULT 0");
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Orders\" ADD COLUMN IF NOT EXISTS \"Version\" integer NOT NULL DEFAULT 1");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"OrderLine\" ADD COLUMN IF NOT EXISTS \"PreparedQuantity\" integer NOT NULL DEFAULT 0");
        }

        var tables = new[] { "DeliveryAssignments", "DriverLocations", "ReceiptChallenges", "OperationReceipts", "CashReceipts" };
        var script = db.Database.GenerateCreateScript();
        foreach (Match match in Regex.Matches(script, @"CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX)\s+.*?;", RegexOptions.Singleline))
        {
            var command = match.Value;
            if (!tables.Any(table => command.Contains("\"" + table + "\"", StringComparison.Ordinal))) continue;
            command = Regex.Replace(command, @"^(CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX))\s+", "$1 IF NOT EXISTS ");
            await db.Database.ExecuteSqlRawAsync(command);
        }
        await AddColumn(db, "Orders", "CreatedBy", "TEXT NOT NULL DEFAULT ''", "text NOT NULL DEFAULT ''");
        await AddColumn(db, "OrderEvents", "ActorId", "TEXT NOT NULL DEFAULT ''", "text NOT NULL DEFAULT ''");
        await AddColumn(db, "ReceiptChallenges", "IssuedBy", "TEXT NOT NULL DEFAULT ''", "text NOT NULL DEFAULT ''");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"CommerceSchemaHistory\" (\"Version\" TEXT NOT NULL PRIMARY KEY, \"AppliedAt\" TEXT NOT NULL)");
        var at = DateTimeOffset.UtcNow.ToString("O");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommerceSchemaHistory\" (\"Version\",\"AppliedAt\") VALUES ({"20261017-operations-v1"},{at}) ON CONFLICT (\"Version\") DO NOTHING");
        await transaction.CommitAsync();
    }

    public static async Task AddColumn(WholesaleDb db, string table, string column, string sqliteDefinition, string postgresDefinition)
    {
        if (db.Database.IsSqlite()) await AddSqliteColumn(db, table, column, sqliteDefinition);
        else { var command = $"ALTER TABLE \"{table}\" ADD COLUMN IF NOT EXISTS \"{column}\" {postgresDefinition}"; await db.Database.ExecuteSqlRawAsync(command); }
    }
    private static async Task AddSqliteColumn(WholesaleDb db, string table, string column, string definition)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        var exists = false;
        var found = false;
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) { exists = true; if (reader.GetString(1) == column) found = true; }
        if (exists && !found) { var sql = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}"; await db.Database.ExecuteSqlRawAsync(sql); }
    }
}
