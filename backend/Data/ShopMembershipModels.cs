using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

// Retain revoked rows: a former member must never become a new standalone shop.
public sealed class ShopMembership
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Role { get; set; } = "VIEWER";
    public string Status { get; set; } = "ACTIVE";
    public int Version { get; set; } = 1;
    public string InvitationId { get; set; } = "";
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ShopMemberInvitation
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "VIEWER";
    public string TokenHash { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string AcceptedBy { get; set; } = "";
    public string RevokedBy { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class ShopAccessEvent
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public long UtcTicks { get; set; } = DateTimeOffset.UtcNow.UtcTicks;
}

public partial class WholesaleDb
{
    public DbSet<ShopMembership> ShopMemberships => Set<ShopMembership>();
    public DbSet<ShopMemberInvitation> ShopMemberInvitations => Set<ShopMemberInvitation>();
    public DbSet<ShopAccessEvent> ShopAccessEvents => Set<ShopAccessEvent>();
}

public static class ShopMembershipModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<ShopMembership>().HasIndex(x => x.AccountId).IsUnique();
        model.Entity<ShopMembership>().HasIndex(x => new { x.WorkspaceId, x.ShopId });
        model.Entity<ShopMembership>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<ShopMemberInvitation>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<ShopMemberInvitation>().HasIndex(x => new { x.WorkspaceId, x.ShopId });
        model.Entity<ShopMemberInvitation>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<ShopAccessEvent>().HasIndex(x => new { x.WorkspaceId, x.ShopId, x.UtcTicks });
    }
}
