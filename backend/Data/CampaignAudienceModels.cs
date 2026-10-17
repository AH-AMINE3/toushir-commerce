using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class CampaignAudience
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string CampaignId { get; set; } = "";
    public string MerchantId { get; set; } = "";
}
public partial class WholesaleDb
{
    public DbSet<CampaignAudience> CampaignAudiences => Set<CampaignAudience>();
}
public static class CampaignAudienceScope
{
    public static IQueryable<Campaign> ForShop(WholesaleDb db, IQueryable<Campaign> campaigns, string shopId) => campaigns.Where(c =>
        !db.CampaignAudiences.Any(a => a.WorkspaceId == c.WorkspaceId && a.CampaignId == c.Id)
        || db.CampaignAudiences.Any(a => a.WorkspaceId == c.WorkspaceId && a.CampaignId == c.Id && a.MerchantId == shopId));
    public static async Task<bool> Allows(WholesaleDb db, Campaign campaign, string shopId) =>
        !await db.CampaignAudiences.AnyAsync(x => x.WorkspaceId == campaign.WorkspaceId && x.CampaignId == campaign.Id)
        || await db.CampaignAudiences.AnyAsync(x => x.WorkspaceId == campaign.WorkspaceId && x.CampaignId == campaign.Id && x.MerchantId == shopId);
}
