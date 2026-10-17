using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public static class OidcSchema
{
    public static async Task Upgrade(WholesaleDb db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (Match match in Regex.Matches(db.Database.GenerateCreateScript(), @"CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX)\s+.*?;", RegexOptions.Singleline))
        {
            if (!new[] { "OpenIddictApplications", "OpenIddictAuthorizations", "OpenIddictScopes", "OpenIddictTokens" }.Any(table => match.Value.Contains("\"" + table + "\"", StringComparison.Ordinal))) continue;
            var command = Regex.Replace(match.Value, @"^(CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX))\s+", "$1 IF NOT EXISTS ");
            await db.Database.ExecuteSqlRawAsync(command);
        }
        var at = DateTimeOffset.UtcNow.ToString("O");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommerceSchemaHistory\" (\"Version\",\"AppliedAt\") VALUES ({"20261017-oidc-v1"},{at}) ON CONFLICT (\"Version\") DO NOTHING");
        await transaction.CommitAsync();
    }
}
