using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class Conversation
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = "";
    public string MerchantId { get; set; } = ""; public string ContextType { get; set; } = "GENERAL"; public string ContextId { get; set; } = "GENERAL";
    public string Subject { get; set; } = ""; public string AssignedTo { get; set; } = ""; public string Status { get; set; } = "OPEN";
    public int Version { get; set; } = 1; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ConversationMessage
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string ConversationId { get; set; } = "";
    public string SenderId { get; set; } = ""; public string SenderName { get; set; } = ""; public string Text { get; set; } = ""; public bool Internal
    {
        get; set;
    }
    public string IdempotencyKey { get; set; } = ""; public string RequestHash { get; set; } = ""; public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CommunicationPreference
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string MerchantId { get; set; } = "";
    public bool MarketingOptIn
    {
        get; set;
    }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class Broadcast
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string Purpose { get; set; } = "";
    public string Title { get; set; } = ""; public string Body { get; set; } = ""; public string MerchantIdsJson { get; set; } = "[]";
    public string IdempotencyKey { get; set; } = ""; public string RequestHash { get; set; } = ""; public string CreatedBy { get; set; } = "";
    public string Status { get; set; } = "DRAFT"; public int Targeted
    {
        get; set;
    }
    public int Sent
    {
        get; set;
    }
    public int Excluded
    {
        get; set;
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset? DispatchedAt
    {
        get; set;
    }
}
public sealed class BroadcastRecipient
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string BroadcastId { get; set; } = "";
    public string MerchantId { get; set; } = ""; public string Status { get; set; } = ""; public string MessageId { get; set; } = "";
}
public sealed class Campaign
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? TargetMerchantIds { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? TargetCount { get; set; }
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string Title { get; set; } = ""; public string Description { get; set; } = "";
    public string Kind { get; set; } = "DISCOUNT"; public string ProductId { get; set; } = ""; public int MinimumQuantity { get; set; } = 1;
    public decimal DiscountPercent
    {
        get; set;
    }
    public string GiftProductId { get; set; } = ""; public int GiftQuantity { get; set; } = 1;
    public int GoalQuantity
    {
        get; set;
    }
    public int UnitFactor { get; set; } = 1; public string RewardId { get; set; } = ""; public int AwardLimit { get; set; } = 1; public int AwardsGranted
    {
        get; set;
    }
    public DateTimeOffset StartsAt
    {
        get; set;
    }
    public DateTimeOffset EndsAt
    {
        get; set;
    }
    public DateTimeOffset? ClaimUntil
    {
        get; set;
    }
    public string Status { get; set; } = "DRAFT"; public string CreatedBy { get; set; } = ""; public string ApprovedBy { get; set; } = "";
    public int Version { get; set; } = 1; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CampaignGrant
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string CampaignId { get; set; } = "";
    public string MerchantId { get; set; } = ""; public string RewardId { get; set; } = ""; public string Status { get; set; } = "GRANTED";
    public int EligibleQuantity
    {
        get; set;
    }
    public string ApprovedBy { get; set; } = ""; public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class LoyaltyWallet
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string MerchantId { get; set; } = "";
    public int Available
    {
        get; set;
    }
    public int Reserved
    {
        get; set;
    }
    public int Spent
    {
        get; set;
    }
    public int CorrectionDue
    {
        get; set;
    }
    public int Version { get; set; } = 1;
}
public sealed class LoyaltyEntry
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string MerchantId { get; set; } = "";
    public string Kind { get; set; } = ""; public string SourceRef { get; set; } = ""; public int AvailableDelta
    {
        get; set;
    }
    public int ReservedDelta
    {
        get; set;
    }
    public int SpentDelta
    {
        get; set;
    }
    public int CorrectionDueDelta
    {
        get; set;
    }
    public int OrderPointDelta
    {
        get; set;
    }
    public string ActorId { get; set; } = ""; public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class RewardItem
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string Name { get; set; } = ""; public string Description { get; set; } = "";
    public string ImageUrl { get; set; } = ""; public int PointsCost
    {
        get; set;
    }
    public int AvailableStock
    {
        get; set;
    }
    public bool IsActive { get; set; } = true; public int Version { get; set; } = 1;
}
public sealed class GiftRequest
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string MerchantId { get; set; } = "";
    public string RewardId { get; set; } = ""; public string RewardName { get; set; } = ""; public string GrantId { get; set; } = ""; public int PointsCost
    {
        get; set;
    }
    public string IdempotencyKey { get; set; } = ""; public string RequestHash { get; set; } = ""; public string Status { get; set; } = "RESERVED";
    public string ProofReference { get; set; } = ""; public string Reason { get; set; } = ""; public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset? FulfilledAt
    {
        get; set;
    }
}
public sealed class Supplier
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string Name { get; set; } = "";
    public string Contact { get; set; } = ""; public string Phone { get; set; } = ""; public string Note { get; set; } = ""; public bool Active { get; set; } = true;
}
public sealed class SupplyOrder
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string SupplierId { get; set; } = ""; public string Number { get; set; } = "";
    public string Status { get; set; } = "DRAFT"; public string CreatedBy { get; set; } = ""; public string ApprovedBy { get; set; } = ""; public string Note { get; set; } = "";
    public string IdempotencyKey { get; set; } = ""; public string RequestHash { get; set; } = ""; public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset ExpectedAt
    {
        get; set;
    }
    public string InvoiceReference { get; set; } = ""; public long? InvoiceMinor
    {
        get; set;
    }
    public string InvoiceMatchStatus { get; set; } = "UNREVIEWED";
}
public sealed class SupplyOrderLine
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string SupplyOrderId { get; set; } = "";
    public string ProductId { get; set; } = ""; public string ProductName { get; set; } = ""; public int OrderedQuantity
    {
        get; set;
    }
    public int ReceivedQuantity
    {
        get; set;
    }
    public int QuarantineQuantity
    {
        get; set;
    }
    public long UnitCostMinor
    {
        get; set;
    }
}
public sealed class SupplyReceipt
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string SupplyOrderId { get; set; } = "";
    public string IdempotencyKey { get; set; } = ""; public string RequestHash { get; set; } = ""; public string ActorId { get; set; } = ""; public string Note { get; set; } = ""; public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class SupplyReceiptLine
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string ReceiptId { get; set; } = ""; public string ProductId { get; set; } = "";
    public int ReceivedQuantity
    {
        get; set;
    }
    public int QuarantineQuantity
    {
        get; set;
    }
    public int InspectedQuantity
    {
        get; set;
    }
    public string LotNumber { get; set; } = ""; public DateTimeOffset? ExpiresAt
    {
        get; set;
    }
    public int Version { get; set; } = 1;
}
public sealed class SupplyInspection
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string ReceiptLineId { get; set; } = "";
    public string IdempotencyKey { get; set; } = ""; public string RequestHash { get; set; } = ""; public int Quantity
    {
        get; set;
    }
    public string Disposition { get; set; } = ""; public string Reason { get; set; } = ""; public string ActorId { get; set; } = ""; public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class EngagementNotice
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string MerchantId { get; set; } = "";
    public string Kind { get; set; } = ""; public string AggregateId { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt
    {
        get; set;
    }
    public int Attempts
    {
        get; set;
    }
}
public sealed class PromotionSnapshot
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string OrderId { get; set; } = ""; public int QuoteVersion
    {
        get; set;
    }
    public string CampaignId { get; set; } = ""; public int CampaignVersion
    {
        get; set;
    }
    public string CampaignTitle { get; set; } = ""; public string Kind { get; set; } = "";
    public string ProductId { get; set; } = ""; public int QuotedQuantity
    {
        get; set;
    }
    public int MinimumQuantity
    {
        get; set;
    }
    public decimal BaseUnitPrice
    {
        get; set;
    }
    public decimal AppliedUnitPrice
    {
        get; set;
    }
    public decimal DiscountPercent
    {
        get; set;
    }
    public long QuotedDiscountMinor
    {
        get; set;
    }
    public long EffectiveDiscountMinor
    {
        get; set;
    }
    public int AcceptedTriggerQuantity
    {
        get; set;
    }
    public int ReturnedTriggerQuantity
    {
        get; set;
    }
    public string GiftProductId { get; set; } = ""; public string GiftProductName { get; set; } = ""; public int GiftQuantity
    {
        get; set;
    }
    public string GiftStockState { get; set; } = "NONE";
    public int GiftAcceptedQuantity
    {
        get; set;
    }
    public int GiftRefusedQuantity
    {
        get; set;
    }
    public string GiftProofReference { get; set; } = "";
    public bool EligibilityReviewRequired
    {
        get; set;
    }
    public string Status { get; set; } = "QUOTED"; public int Version { get; set; } = 1; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class PromotionEvent
{
    public string Id { get; set; } = IdGen.New(); public string WorkspaceId { get; set; } = ""; public string PromotionSnapshotId { get; set; } = "";
    public string Kind { get; set; } = ""; public int Quantity
    {
        get; set;
    }
    public long DiscountMinor
    {
        get; set;
    }
    public string IdempotencyKey { get; set; } = ""; public string RequestHash { get; set; } = "";
    public string ActorId { get; set; } = ""; public string Note { get; set; } = ""; public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public partial class WholesaleDb
{
    public DbSet<PromotionSnapshot> PromotionSnapshots => Set<PromotionSnapshot>(); public DbSet<PromotionEvent> PromotionEvents => Set<PromotionEvent>();
    public DbSet<EngagementNotice> EngagementNotices => Set<EngagementNotice>();
    public DbSet<Conversation> Conversations => Set<Conversation>(); public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<CommunicationPreference> CommunicationPreferences => Set<CommunicationPreference>(); public DbSet<Broadcast> Broadcasts => Set<Broadcast>(); public DbSet<BroadcastRecipient> BroadcastRecipients => Set<BroadcastRecipient>();
    public DbSet<Campaign> Campaigns => Set<Campaign>(); public DbSet<CampaignGrant> CampaignGrants => Set<CampaignGrant>(); public DbSet<LoyaltyWallet> LoyaltyWallets => Set<LoyaltyWallet>(); public DbSet<LoyaltyEntry> LoyaltyEntries => Set<LoyaltyEntry>();
    public DbSet<RewardItem> RewardItems => Set<RewardItem>(); public DbSet<GiftRequest> GiftRequests => Set<GiftRequest>(); public DbSet<Supplier> Suppliers => Set<Supplier>(); public DbSet<SupplyOrder> SupplyOrders => Set<SupplyOrder>();
    public DbSet<SupplyOrderLine> SupplyOrderLines => Set<SupplyOrderLine>(); public DbSet<SupplyReceipt> SupplyReceipts => Set<SupplyReceipt>(); public DbSet<SupplyReceiptLine> SupplyReceiptLines => Set<SupplyReceiptLine>(); public DbSet<SupplyInspection> SupplyInspections => Set<SupplyInspection>();
    partial void ConfigureModuleModels(ModelBuilder m)
    {
        WarehouseSchema.Configure(m);
        m.Entity<PromotionSnapshot>().HasIndex(x => new { x.OrderId, x.QuoteVersion, x.ProductId }).IsUnique();
        m.Entity<PromotionSnapshot>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<PromotionSnapshot>().Property(x => x.BaseUnitPrice).HasPrecision(18, 2);
        m.Entity<PromotionSnapshot>().Property(x => x.AppliedUnitPrice).HasPrecision(18, 2);
        m.Entity<PromotionSnapshot>().Property(x => x.DiscountPercent).HasPrecision(5, 2);
        m.Entity<PromotionEvent>().HasIndex(x => new { x.PromotionSnapshotId, x.IdempotencyKey }).IsUnique();
        m.Entity<Conversation>().HasIndex(x => new { x.WorkspaceId, x.MerchantId, x.ContextType, x.ContextId }).IsUnique();
        m.Entity<Conversation>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<ConversationMessage>().HasIndex(x => new { x.ConversationId, x.SenderId, x.IdempotencyKey }).IsUnique();
        m.Entity<CommunicationPreference>().HasIndex(x => new { x.WorkspaceId, x.MerchantId }).IsUnique();
        m.Entity<Broadcast>().HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        m.Entity<BroadcastRecipient>().HasIndex(x => new { x.BroadcastId, x.MerchantId }).IsUnique();
        m.Entity<Campaign>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<Campaign>().Property(x => x.DiscountPercent).HasPrecision(5, 2);
        m.Entity<CampaignGrant>().HasIndex(x => new { x.CampaignId, x.MerchantId }).IsUnique();
        m.Entity<LoyaltyWallet>().HasIndex(x => new { x.WorkspaceId, x.MerchantId }).IsUnique();
        m.Entity<LoyaltyWallet>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<LoyaltyEntry>().HasIndex(x => new { x.WorkspaceId, x.MerchantId, x.SourceRef });
        m.Entity<RewardItem>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<GiftRequest>().HasIndex(x => new { x.WorkspaceId, x.MerchantId, x.IdempotencyKey }).IsUnique();
        m.Entity<GiftRequest>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<SupplyOrder>().HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        m.Entity<SupplyOrder>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<SupplyOrderLine>().HasIndex(x => new { x.SupplyOrderId, x.ProductId }).IsUnique();
        m.Entity<SupplyReceipt>().HasIndex(x => new { x.SupplyOrderId, x.IdempotencyKey }).IsUnique();
        m.Entity<SupplyReceiptLine>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<SupplyInspection>().HasIndex(x => new { x.ReceiptLineId, x.IdempotencyKey }).IsUnique();
    }
}
