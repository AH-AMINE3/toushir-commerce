using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Additive upgrade for databases created before the engagement/supply module; respects the configured PostgreSQL search_path.</summary>
public static class EngagementSchema
{
    public static readonly string[] Tables = ["Conversations", "ConversationMessages", "CommunicationPreferences", "Broadcasts", "BroadcastRecipients", "Campaigns", "CampaignGrants", "LoyaltyWallets", "LoyaltyEntries", "RewardItems", "GiftRequests", "Suppliers", "SupplyOrders", "SupplyOrderLines", "SupplyReceipts", "SupplyReceiptLines", "SupplyInspections", "EngagementNotices", "PromotionSnapshots", "PromotionEvents", "SupplierDocuments", "SupplierConfirmations", "SupplyProcurementEvents", "PriceSegments", "MerchantPriceSegments", "TariffRules", "PricingEvents", "OrderPricingSnapshots"];
    public static async Task Upgrade(WholesaleDb db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (Match match in Regex.Matches(db.Database.GenerateCreateScript(), @"CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX)\s+.*?;", RegexOptions.Singleline))
        {
            var command = match.Value;
            if (!Tables.Any(table => command.Contains("\"" + table + "\"", StringComparison.Ordinal))) continue;
            command = Regex.Replace(command, @"^(CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX))\s+", "$1 IF NOT EXISTS ");
            await db.Database.ExecuteSqlRawAsync(command);
        }
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"CommerceSchemaHistory\" (\"Version\" TEXT NOT NULL PRIMARY KEY, \"AppliedAt\" TEXT NOT NULL)");
        var at = DateTimeOffset.UtcNow.ToString("O");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommerceSchemaHistory\" (\"Version\",\"AppliedAt\") VALUES ({"20261017-engagement-v1"},{at}) ON CONFLICT (\"Version\") DO NOTHING");
        await transaction.CommitAsync();
    }
}
