using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Toushir.Wholesale;

public static partial class DatabaseConfiguration
{
    public static async Task InitializeDatabase(WholesaleDb db, IConfiguration configuration)
    {
        var schema = ValidateSchema(configuration["Database:Schema"]);
        if (db.Database.IsNpgsql() && schema == null) throw new InvalidOperationException("PostgreSQL requires a dedicated Database:Schema.");
        if (!db.Database.IsNpgsql()) { if(db.Database.GetMigrations().Any()&&(!await db.GetService<IRelationalDatabaseCreator>().HasTablesAsync()||(await db.Database.GetAppliedMigrationsAsync()).Any()))await db.Database.MigrateAsync();else await db.Database.EnsureCreatedAsync(); return; }
        // The connection search_path contains only this validated schema. Public tables are never inspected or changed.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"toushir-schema:" + schema}))");
        var createSchema = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
        await db.Database.ExecuteSqlRawAsync(createSchema);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @schema AND table_type = 'BASE TABLE'";
        var parameter = command.CreateParameter(); parameter.ParameterName = "schema"; parameter.Value = schema; command.Parameters.Add(parameter);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (count == 0) {
            var script=db.Database.GetMigrations().Any()?db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript(options:Microsoft.EntityFrameworkCore.Migrations.MigrationsSqlGenerationOptions.NoTransactions):db.Database.GenerateCreateScript();
            await db.Database.ExecuteSqlRawAsync(script);
        }
        else
        {
            command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @schema AND table_name = 'Orders'";
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
                throw new InvalidOperationException("The dedicated commerce schema contains unrelated or incomplete tables. Review migrations instead of initializing over them.");
        }
        await transaction.CommitAsync();
        if ((await db.Database.GetAppliedMigrationsAsync()).Any()) await db.Database.MigrateAsync();
    }
}
