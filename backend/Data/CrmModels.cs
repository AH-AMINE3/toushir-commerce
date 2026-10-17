using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class CrmPortfolio
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string AdvisorId { get; set; } = "";
    public int Version { get; set; } = 1;
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CrmTask
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public string Kind { get; set; } = "CONTACT";
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "NORMAL";
    public string Status { get; set; } = "OPEN";
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? VisitEnd { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CompletedBy { get; set; } = "";
    public DateTimeOffset? CompletedAt { get; set; }
    public int Version { get; set; } = 1;
}

// Contact/visit outcomes are reports by the named person, not proof of a call or physical attendance.
public sealed class CrmContact
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string? TaskId { get; set; }
    public string Kind { get; set; } = "CONTACT";
    public string Outcome { get; set; } = "";
    public string Note { get; set; } = "";
    public string NextAction { get; set; } = "";
    public DateTimeOffset ObservedAt { get; set; }
    public string ActorId { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

// One-use authority for an exact RFQ. It cannot accept a quote, reserve stock or record payment.
public sealed class CrmAssistance
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string AdvisorId { get; set; } = "";
    public int PortfolioVersion { get; set; }
    public string IssuedBy { get; set; } = "";
    public string IssuerRole { get; set; } = "";
    public int IssuerMembershipVersion { get; set; }
    public string AddressId { get; set; } = "";
    public string AddressJson { get; set; } = "{}";
    public string LinesJson { get; set; } = "[]";
    public string ProductSnapshotsJson { get; set; } = "[]";
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string RevokedBy { get; set; } = "";
    public DateTimeOffset? UsedAt { get; set; }
    public string UsedBy { get; set; } = "";
    public string UseKeyHash { get; set; } = "";
    public string? OrderId { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class CrmEvent
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string Details { get; set; } = "{}";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public long UtcTicks { get; set; } = DateTimeOffset.UtcNow.UtcTicks;
}

public partial class WholesaleDb
{
    public DbSet<CrmPortfolio> CrmPortfolios => Set<CrmPortfolio>();
    public DbSet<CrmTask> CrmTasks => Set<CrmTask>();
    public DbSet<CrmContact> CrmContacts => Set<CrmContact>();
    public DbSet<CrmAssistance> CrmAssistances => Set<CrmAssistance>();
    public DbSet<CrmEvent> CrmEvents => Set<CrmEvent>();
}

public static class CrmModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<CrmPortfolio>().HasIndex(x => new { x.WorkspaceId, x.ShopId }).IsUnique();
        model.Entity<CrmPortfolio>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<CrmTask>().HasIndex(x => new { x.WorkspaceId, x.ShopId });
        model.Entity<CrmTask>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<CrmContact>().HasIndex(x => new { x.WorkspaceId, x.ShopId });
        model.Entity<CrmAssistance>().HasIndex(x => new { x.WorkspaceId, x.ShopId });
        model.Entity<CrmAssistance>().HasIndex(x => x.OrderId).IsUnique();
        model.Entity<CrmAssistance>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<CrmEvent>().HasIndex(x => new { x.WorkspaceId, x.ShopId, x.UtcTicks });
    }
}
