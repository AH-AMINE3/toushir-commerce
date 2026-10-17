using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class DeliveryRoute
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string VehicleReference { get; set; } = "";
    public int? CapacityUnits { get; set; }
    public string Status { get; set; } = "PLANNED";
    public int Version { get; set; } = 1;
    public decimal OpeningFloat { get; set; }
    public string CashState { get; set; } = "OPEN";
    public decimal? DeclaredAmount { get; set; }
    public decimal? CountedAmount { get; set; }
    public decimal? ExpectedAtDeclaration { get; set; }
    public decimal? Shortage { get; set; }
    public decimal? Surplus { get; set; }
    public string DeclarationNote { get; set; } = "";
    public string FinanceNote { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string? CountedBy { get; set; }
    public string? ReconciledBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DepartedAt { get; set; }
    public DateTimeOffset? DeclaredAt { get; set; }
    public DateTimeOffset? CountedAt { get; set; }
    public DateTimeOffset? ReconciledAt { get; set; }
}

public sealed class RouteCustodyChallenge
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string RouteId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string WarehouseActorId { get; set; } = "";
    public int Version { get; set; }
    public string ManifestHash { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public sealed class RouteCustodyReceipt
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string RouteId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string WarehouseActorId { get; set; } = "";
    public int AcceptedVersion { get; set; }
    public string ManifestJson { get; set; } = "[]";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public partial class WholesaleDb
{
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<RouteCustodyChallenge> RouteCustodyChallenges => Set<RouteCustodyChallenge>();
    public DbSet<RouteCustodyReceipt> RouteCustodyReceipts => Set<RouteCustodyReceipt>();
}

public record CreateRouteInput(string Name, string DriverId, List<string> OrderIds, decimal OpeningFloat, string? VehicleReference,int? CapacityUnits=null);
public record ReorderRouteInput(int Version,List<string> OrderIds,int CapacityUnits);
public record DepotReturnLine(string ProductId,int Quantity);
public record DepotReturnInput(string OrderId,int Version,List<DepotReturnLine> Items,string? Note);
public record CustodyInput(int Version, string Challenge);
public record DeclareCashInput(int Version, decimal DeclaredAmount, string? Note);
public record CountCashInput(int Version, decimal CountedAmount, string? Note);
public record ReconcileRouteInput(int Version, string? Note);
