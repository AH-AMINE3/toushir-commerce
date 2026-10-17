using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class PriceSegment
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MerchantPriceSegment
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string MerchantId { get; set; } = "";
    public string SegmentId { get; set; } = "";
    public int Version { get; set; } = 1;
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TariffRule
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string Scope { get; set; } = "STANDARD";
    public string TargetId { get; set; } = "";
    public int MinimumQuantity { get; set; } = 1;
    public int? MaximumQuantity { get; set; }
    public long UnitPriceMinor { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool AllowCampaigns { get; set; }
    public string Reference { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "DRAFT";
    public int Version { get; set; } = 1;
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ReviewedBy { get; set; } = "";
    public string ReviewReason { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public string WithdrawnBy { get; set; } = "";
    public string WithdrawalReason { get; set; } = "";
    public string SupersedesId { get; set; } = "";
    public int? SupersedesVersion { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
}

public sealed class PricingEvent
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string AggregateId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public long UtcTicks { get; set; } = DateTimeOffset.UtcNow.UtcTicks;
}

public sealed class OrderPricingSnapshot
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public int QuoteVersion { get; set; }
    public string MerchantId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public string Source { get; set; } = "";
    public string ResolutionSource { get; set; } = "";
    public string RuleId { get; set; } = "";
    public int RuleVersion { get; set; }
    public long? ExpectedUnitPriceMinor { get; set; }
    public long BaseUnitPriceMinor { get; set; }
    public long AppliedUnitPriceMinor { get; set; }
    public bool CampaignsAllowed { get; set; }
    public string OverrideReason { get; set; } = "";
    public string TermsJson { get; set; } = "{}";
    public DateTimeOffset ResolvedAt { get; set; } = DateTimeOffset.UtcNow;
}

public partial class WholesaleDb
{
    public DbSet<PriceSegment> PriceSegments => Set<PriceSegment>();
    public DbSet<MerchantPriceSegment> MerchantPriceSegments => Set<MerchantPriceSegment>();
    public DbSet<TariffRule> TariffRules => Set<TariffRule>();
    public DbSet<PricingEvent> PricingEvents => Set<PricingEvent>();
    public DbSet<OrderPricingSnapshot> OrderPricingSnapshots => Set<OrderPricingSnapshot>();
}

public static class PricingModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<PriceSegment>().HasIndex(x => new { x.WorkspaceId, x.NormalizedName }).IsUnique();
        model.Entity<PriceSegment>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<MerchantPriceSegment>().HasIndex(x => new { x.WorkspaceId, x.MerchantId }).IsUnique();
        model.Entity<MerchantPriceSegment>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<TariffRule>().HasIndex(x => new { x.WorkspaceId, x.ProductId, x.Status });
        model.Entity<TariffRule>().HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        model.Entity<TariffRule>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<PricingEvent>().HasIndex(x => new { x.WorkspaceId, x.AggregateId });
        model.Entity<PricingEvent>().HasIndex(x => new { x.WorkspaceId, x.UtcTicks });
        model.Entity<OrderPricingSnapshot>().HasIndex(x => new { x.WorkspaceId, x.OrderId, x.QuoteVersion, x.ProductId }).IsUnique();
    }
}
