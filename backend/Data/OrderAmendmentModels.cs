using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Before and proposed terms are append-only contract evidence. A proposal never holds stock.</summary>
public sealed class OrderAmendment
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string MerchantId { get; set; } = "";
    public int Version { get; set; } = 1;
    public int BaseOrderVersion { get; set; }
    public string Status { get; set; } = "PENDING";
    public string Reason { get; set; } = "";
    public string ProposedBy { get; set; } = "";
    public string ProposerName { get; set; } = "";
    public DateTimeOffset ProposedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string LinesJson { get; set; } = "[]";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
    public string TermsHash { get; set; } = "";
    public string DecidedBy { get; set; } = "";
    public string DecisionName { get; set; } = "";
    public DateTimeOffset? DecidedAt { get; set; }
    public string DecisionReason { get; set; } = "";
    public string DecisionKey { get; set; } = "";
    public string DecisionRequestHash { get; set; } = "";
    public int? AcceptedOrderVersion { get; set; }
}

public partial class WholesaleDb
{
    public DbSet<OrderAmendment> OrderAmendments => Set<OrderAmendment>();
}

public static class OrderAmendmentModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<OrderAmendment>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<OrderAmendment>().HasIndex(x => new { x.WorkspaceId, x.OrderId, x.IdempotencyKey }).IsUnique();
        model.Entity<OrderAmendment>().HasIndex(x => new { x.WorkspaceId, x.OrderId }).IsUnique().HasFilter("\"Status\" = 'PENDING'");
        model.Entity<OrderAmendment>().HasIndex(x => new { x.WorkspaceId, x.MerchantId });
    }
}
