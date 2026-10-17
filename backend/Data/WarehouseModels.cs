using Microsoft.EntityFrameworkCore;
namespace Toushir.Wholesale;

public sealed class LotProductControl
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string ProductId { get; set; } = "";
    public string ActivatedBy { get; set; } = ""; public string CountReference { get; set; } = ""; public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class WarehouseLot
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string ProductId { get; set; } = ""; public string LotNumber { get; set; } = "";
    public DateTimeOffset? ExpiresAt
    {
        get; set;
    }
    public int OnHand
    {
        get; set;
    }
    public int Reserved
    {
        get; set;
    }
    public int Quarantined
    {
        get; set;
    }
    public int InitialQuantity
    {
        get; set;
    }
    public string SourceKey { get; set; } = ""; public string SourceRef { get; set; } = ""; public int Version { get; set; } = 1; public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class LotAllocation
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string OrderId { get; set; } = ""; public string OrderLineId { get; set; } = "";
    public string PromotionSnapshotId { get; set; } = ""; public string ProductId { get; set; } = ""; public string LotId { get; set; } = ""; public string AllocationKey { get; set; } = "";
    public int Quantity
    {
        get; set;
    }
    public int PickedQuantity
    {
        get; set;
    }
    public string Status { get; set; } = "RESERVED"; public string WaveId { get; set; } = ""; public int Version { get; set; } = 1;
}
public sealed class PickWave
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string Name { get; set; } = ""; public string Status { get; set; } = "OPEN";
    public string CreatedBy { get; set; } = ""; public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class WarehouseOperation
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string ActorId { get; set; } = ""; public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = ""; public string Kind { get; set; } = ""; public string ResultId { get; set; } = ""; public int Quantity
    {
        get; set;
    }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public partial class WholesaleDb
{
    public DbSet<LotProductControl> LotProductControls => Set<LotProductControl>(); public DbSet<WarehouseLot> WarehouseLots => Set<WarehouseLot>();
    public DbSet<LotAllocation> LotAllocations => Set<LotAllocation>(); public DbSet<PickWave> PickWaves => Set<PickWave>(); public DbSet<WarehouseOperation> WarehouseOperations => Set<WarehouseOperation>();
}
