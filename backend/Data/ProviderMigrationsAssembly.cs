using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Npgsql;

namespace Toushir.Wholesale;

public interface ISchemaMigration { string Schema {get;set;} }
// Isolate migrations/snapshots by provider, retaining exact provider decimal/time mappings.
#pragma warning disable EF1001,CS9107
public sealed class ProviderMigrationsAssembly(ICurrentDbContext context, IDbContextOptions options, IMigrationsIdGenerator generator, IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
    : MigrationsAssembly(context, options, generator, logger)
{
    private string ProviderNamespace => context.Context.Database.IsNpgsql() ? ".Migrations.Postgres" : ".Migrations.Sqlite";
    private string Schema => DatabaseConfiguration.ValidateSchema(new NpgsqlConnectionStringBuilder(options.Extensions.OfType<RelationalOptionsExtension>().First().ConnectionString).SearchPath) ?? throw new InvalidOperationException("Dedicated PostgreSQL schema missing.");
    public override IReadOnlyDictionary<string, TypeInfo> Migrations => base.Migrations.Where(item => item.Value.Namespace?.Contains(ProviderNamespace, StringComparison.Ordinal) == true).ToDictionary(item => item.Key, item => item.Value);
    public override ModelSnapshot? ModelSnapshot {
        get {
            var snapshot=Assembly.DefinedTypes.Where(type=>typeof(ModelSnapshot).IsAssignableFrom(type.AsType())&&!type.IsAbstract&&type.Namespace?.Contains(ProviderNamespace,StringComparison.Ordinal)==true).Select(type=>(ModelSnapshot?)Activator.CreateInstance(type.AsType())).SingleOrDefault();
            if(snapshot is ISchemaMigration aware)aware.Schema=Schema;
            return snapshot;
        }
    }
    public override Migration CreateMigration(TypeInfo migrationClass,string activeProvider){var migration=base.CreateMigration(migrationClass,activeProvider);if(migration is ISchemaMigration aware)aware.Schema=Schema;return migration;}
}
#pragma warning restore EF1001,CS9107
