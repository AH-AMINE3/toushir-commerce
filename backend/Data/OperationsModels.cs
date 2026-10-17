namespace Toushir.Wholesale;

public sealed class DeliveryAssignment
{
    public int StopRank { get; set; }
    public string RouteId { get; set; } = "";
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string AssignedBy { get; set; } = "";
    public string Status { get; set; } = "ASSIGNED";
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PickedUpAt { get; set; }
    public DateTimeOffset? ArrivedAt { get; set; }
    public string FailureReason { get; set; } = "";
}

public sealed class DriverLocation
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Consent { get; set; }
}

public sealed class ReceiptChallenge
{
    public string RouteId { get; set; } = "";
    public int RouteVersion { get; set; }
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string MerchantId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public int Version { get; set; }
    public string TokenHash { get; set; } = "";
    public string ItemsHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

public sealed class OperationReceipt
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string MerchantId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string ChallengeId { get; set; } = "";
    public int AcceptedVersion { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CashReceipt
{
    public string RouteId { get; set; } = "";
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Tendered { get; set; }
    public decimal Change { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
