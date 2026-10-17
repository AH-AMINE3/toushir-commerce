using Microsoft.EntityFrameworkCore;
namespace Toushir.Wholesale;

// A recall is anchored to a counted physical identity, never to legacy aggregate stock.
public sealed class RecallCase
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string LotId { get; set; } = "";
    public string IdentityKey { get; set; } = "";
    public string LotNumber { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Reason { get; set; } = "";
    public string SafetyInstruction { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    // Administrative closure never authorizes redistribution of a recalled batch.
    public bool ContainmentActive { get; set; } = true;
    public int UnknownUntrackedOrderCount { get; set; }
    public int UnknownUntrackedQuantity { get; set; }
    public int Version { get; set; } = 1;
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ClosedBy { get; set; } = "";
    public DateTimeOffset? ClosedAt { get; set; }
    public string ClosureNote { get; set; } = "";
    public string UnknownScopeReason { get; set; } = "";
}
public sealed class RecallPosition
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string RecallId { get; set; } = "";
    public string PositionKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public string LotId { get; set; } = "";
    public string AllocationId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string SnapshotOrderStatus { get; set; } = "";
    public int Quantity { get; set; }
    public int RecoveredQuantity { get; set; }
    public int ExceptionQuantity { get; set; }
    public bool RequiresRecovery { get; set; } = true;
    public string ExceptionReason { get; set; } = "";
    public string ExceptionReference { get; set; } = "";
    public string ExceptionBy { get; set; } = "";
    public DateTimeOffset? ExceptionAt { get; set; }
    public int Version { get; set; } = 1;
}
public sealed class RecallNotice
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string RecallId { get; set; } = "";
    public string RecipientId { get; set; } = "";
    public string RecipientRole { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ReceivedBy { get; set; } = "";
    public DateTimeOffset? ReceivedAt { get; set; }
    public int Version { get; set; } = 1;
}
public sealed class RecallEvent
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string RecallId { get; set; } = "";
    public string PositionId { get; set; } = "";
    public string WarehouseLotId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Kind { get; set; } = "";
    public int Quantity { get; set; }
    public string Destination { get; set; } = "";
    public string ProofReference { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class RecallOperation
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string RecallId { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public partial class WholesaleDb
{
    public DbSet<RecallCase> RecallCases => Set<RecallCase>();
    public DbSet<RecallPosition> RecallPositions => Set<RecallPosition>();
    public DbSet<RecallNotice> RecallNotices => Set<RecallNotice>();
    public DbSet<RecallEvent> RecallEvents => Set<RecallEvent>();
    public DbSet<RecallOperation> RecallOperations => Set<RecallOperation>();
}
public static class RecallModelConfiguration
{
    public static void Configure(ModelBuilder m)
    {
        m.Entity<RecallCase>().HasIndex(x => new { x.WorkspaceId, x.IdentityKey }).IsUnique();
        m.Entity<RecallCase>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<RecallPosition>().HasIndex(x => new { x.RecallId, x.PositionKey }).IsUnique();
        m.Entity<RecallPosition>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<RecallNotice>().HasIndex(x => new { x.RecallId, x.RecipientRole, x.RecipientId }).IsUnique();
        m.Entity<RecallNotice>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<RecallEvent>().HasIndex(x => new { x.WorkspaceId, x.RecallId });
        m.Entity<RecallOperation>().HasIndex(x => new { x.WorkspaceId, x.ActorId, x.IdempotencyKey }).IsUnique();
    }
}
