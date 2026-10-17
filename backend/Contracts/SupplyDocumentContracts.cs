namespace Toushir.Wholesale;

public record UploadSupplierDocumentInput(int OrderVersion, string Kind, string Reference,
    DateTimeOffset DocumentDate, string FileName, string ContentBase64, string IdempotencyKey,
    string? ReceiptId = null, string? SupersedesId = null, int? SupersededVersion = null);
public record VoidSupplierDocumentInput(int Version, string Reason);
public record SupplierConfirmedLineInput(string ProductId, int Quantity, long UnitCostMinor);
public record RecordSupplierConfirmationInput(int OrderVersion, string DocumentId, string SupplierReference,
    DateTimeOffset ExpectedAt, List<SupplierConfirmedLineInput> Items, string IdempotencyKey);
public record ReviewSupplierConfirmationInput(int OrderVersion, int Version, string Decision, string Reason);
public record CloseSupplyOrderInput(int Version, string Reason);
