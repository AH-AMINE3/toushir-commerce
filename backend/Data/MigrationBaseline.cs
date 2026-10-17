using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Toushir.Wholesale;

public static class MigrationBaseline
{
    /// <summary>Adopt the first EF baseline only after every expected table and column exists in this provider/schema.</summary>
    public static async Task AdoptExisting(WholesaleDb db)
    {
        var migrations = db.Database.GetMigrations().ToList();
        if (migrations.Count == 0 || (await db.Database.GetAppliedMigrationsAsync()).Any()) return;
        var schema = db.Model.GetDefaultSchema() ?? "public";
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var table in db.Model.GetRelationalModel().Tables)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = transaction.GetDbTransaction();
            if (db.Database.IsSqlite()) command.CommandText = $"PRAGMA table_info(\"{table.Name.Replace("\"", "\"\"")}\")";
            else
            {
                command.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema=@schema AND table_name=@table";
                var schemaParameter = command.CreateParameter(); schemaParameter.ParameterName = "schema"; schemaParameter.Value = table.Schema ?? schema; command.Parameters.Add(schemaParameter);
                var tableParameter = command.CreateParameter(); tableParameter.ParameterName = "table"; tableParameter.Value = table.Name; command.Parameters.Add(tableParameter);
            }
            var columns = new HashSet<string>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync()) columns.Add(reader.GetString(db.Database.IsSqlite() ? 1 : 0));
            if (table.Columns.Any(column => !columns.Contains(column.Name))) throw new InvalidOperationException($"Cannot baseline incomplete commerce table {table.Name}. Apply and verify the additive upgrade first.");
        }
        var history = db.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        foreach(var migration in migrations)await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migration, "10.0.9")));
        await transaction.CommitAsync();
    }
}
