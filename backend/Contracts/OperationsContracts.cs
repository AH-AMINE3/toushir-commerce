namespace Toushir.Wholesale;

public record PrepareLineInput(string ProductId, int PreparedQuantity);
public record PrepareInput(List<PrepareLineInput> Items, int Version);
public record AssignDriverInput(string DriverId, int Version);
public record VersionInput(int Version);
public record CourierStatusInput(string Status, string? Reason);
public record DriverLocationInput(double Latitude, double Longitude, double Accuracy, DateTimeOffset CapturedAt, bool Consent);
public record ReceiptProofInput(int Version, List<DeliveryLineInput> Items);
public record CompleteDeliveryInput(string Challenge, List<DeliveryLineInput> Items, string? Note);
public record CourierCollectionInput(decimal Amount, decimal Tendered, decimal Change, string Method, string IdempotencyKey, string? Reference);
public record CreateTeamInput(string Name, string Email, string Password, string Role, string? Phone);
