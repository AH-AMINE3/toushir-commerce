using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Toushir.Wholesale;

public static class DatabaseConfiguration {
 public static void Configure(DbContextOptionsBuilder options,string? databaseUrl,string sqlitePath) {
  if(string.IsNullOrWhiteSpace(databaseUrl)) {
   options.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder {
    DataSource=sqlitePath,DefaultTimeout=15,ForeignKeys=true
   }.ConnectionString);
   return;
  }
  options.UseNpgsql(PostgresConnectionString(databaseUrl));
 }

 public static string PostgresConnectionString(string databaseUrl) {
  // Use the provider's builder: URI-decoded credentials may contain semicolons,
  // equals signs or colons and must never be concatenated into a connection string.
  if(!Uri.TryCreate(databaseUrl,UriKind.Absolute,out var uri)||
     (uri.Scheme!="postgres"&&uri.Scheme!="postgresql")||
     string.IsNullOrWhiteSpace(uri.Host)||!string.IsNullOrEmpty(uri.Fragment))
   throw new InvalidOperationException("DATABASE_URL must be a valid PostgreSQL URI.");
  var separator=uri.UserInfo.IndexOf(':');
  var username=Uri.UnescapeDataString(separator<0?uri.UserInfo:uri.UserInfo[..separator]);
  var password=separator<0?"":Uri.UnescapeDataString(uri.UserInfo[(separator+1)..]);
  var database=Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
  if(username.Length==0||database.Length==0||database.Contains('/'))
   throw new InvalidOperationException("DATABASE_URL requires a username and database name.");
  var sslMode=SslMode.Require;
  foreach(var item in uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)) {
   var parts=item.Split('=',2);
   var key=Uri.UnescapeDataString(parts[0]);
   if(!key.Equals("sslmode",StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("DATABASE_URL contains an unsupported query setting.");
   var value=parts.Length>1?Uri.UnescapeDataString(parts[1]):"";
   sslMode=value.Replace("-","").ToLowerInvariant() switch {
    "disable"=>SslMode.Disable,"allow"=>SslMode.Allow,"prefer"=>SslMode.Prefer,
    "require"=>SslMode.Require,"verifyca"=>SslMode.VerifyCA,"verifyfull"=>SslMode.VerifyFull,
    _=>throw new InvalidOperationException("DATABASE_URL has an invalid sslmode setting.")
   };
  }
  return new NpgsqlConnectionStringBuilder {
   Host=uri.Host,Port=uri.Port>0?uri.Port:5432,Database=database,
   Username=username,Password=password,SslMode=sslMode,
   Timeout=15,CommandTimeout=30,MaxPoolSize=20,
   ApplicationName="Toushir.Wholesale",IncludeErrorDetail=false
  }.ConnectionString;
 }
}
