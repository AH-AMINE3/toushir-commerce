namespace Toushir.Wholesale;

public record CountedLotInput(string LotNumber, DateTimeOffset? ExpiresAt, int Quantity, int Quarantined = 0);
public record ActivateLotsInput(string ProductId, int? ExpectedPhysicalStock, string Reason, string ProofReference, string IdempotencyKey, List<CountedLotInput> Lots);
public record CreatePickWaveInput(string Name, List<string> OrderIds, string IdempotencyKey);
public record ScanPickInput(int Version, string Barcode, string? LotNumber, int Quantity, string IdempotencyKey);
public record CountLotInput(int Version, int Quantity, string Reason, string ProofReference, string IdempotencyKey);
public record LotQualityInput(int Version, int Quantity, string Disposition, string Reason, string IdempotencyKey);
public record IdentifyReturnLotInput(string SourceType, string SourceId, int Quantity, string LotNumber, DateTimeOffset? ExpiresAt, string ProofReference, string IdempotencyKey);
