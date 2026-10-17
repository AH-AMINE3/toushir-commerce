using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class SupplierDocument
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string SupplyOrderId { get; set; } = "";
    public string ReceiptId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Reference { get; set; } = "";
    public string FileName { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public int ByteLength { get; set; }
    public string Sha256 { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public string SupersedesId { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string VoidReason { get; set; } = "";
    public string VoidedBy { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTimeOffset DocumentDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VoidedAt { get; set; }
}

public sealed class SupplierConfirmation
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string SupplyOrderId { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public string SupplierReference { get; set; } = "";
    public string ItemsJson { get; set; } = "[]";
    public string OriginalTermsHash { get; set; } = "";
    public string Status { get; set; } = "RECORDED";
    public string RecordedBy { get; set; } = "";
    public string ReviewedBy { get; set; } = "";
    public string ReviewReason { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTimeOffset ExpectedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class SupplyProcurementEvent
{
    public string Id { get; set; } = IdGen.New();
    public string WorkspaceId { get; set; } = "";
    public string SupplyOrderId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string ReferenceId { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public partial class WholesaleDb
{
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<SupplierConfirmation> SupplierConfirmations => Set<SupplierConfirmation>();
    public DbSet<SupplyProcurementEvent> SupplyProcurementEvents => Set<SupplyProcurementEvent>();
}

public static class SupplyDocumentModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<SupplierDocument>().HasIndex(x => new { x.WorkspaceId, x.SupplyOrderId });
        model.Entity<SupplierDocument>().HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        model.Entity<SupplierDocument>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<SupplierConfirmation>().HasIndex(x => new { x.WorkspaceId, x.SupplyOrderId });
        model.Entity<SupplierConfirmation>().HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        model.Entity<SupplierConfirmation>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<SupplyProcurementEvent>().HasIndex(x => new { x.WorkspaceId, x.SupplyOrderId });
    }
}
