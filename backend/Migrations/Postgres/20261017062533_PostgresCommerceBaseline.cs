using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class PostgresCommerceBaseline : Migration,Toushir.Wholesale.ISchemaMigration
    {
        public string Schema {get;set;}="commerce_ecosystem";
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: Schema);

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ShopName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Wilaya = table.Column<string>(type: "text", nullable: false),
                    Commune = table.Column<string>(type: "text", nullable: false),
                    Street = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Audit",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BroadcastRecipients",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    BroadcastId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastRecipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Broadcasts",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    MerchantIdsJson = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Targeted = table.Column<int>(type: "integer", nullable: false),
                    Sent = table.Column<int>(type: "integer", nullable: false),
                    Excluded = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Broadcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignGrants",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    RewardId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EligibleQuantity = table.Column<int>(type: "integer", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    GiftProductId = table.Column<string>(type: "text", nullable: false),
                    GiftQuantity = table.Column<int>(type: "integer", nullable: false),
                    GoalQuantity = table.Column<int>(type: "integer", nullable: false),
                    UnitFactor = table.Column<int>(type: "integer", nullable: false),
                    RewardId = table.Column<string>(type: "text", nullable: false),
                    AwardLimit = table.Column<int>(type: "integer", nullable: false),
                    AwardsGranted = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashReceipts",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RouteId = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Tendered = table.Column<decimal>(type: "numeric", nullable: false),
                    Change = table.Column<decimal>(type: "numeric", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ResolutionNote = table.Column<string>(type: "text", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false),
                    InspectedQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreditedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationPreferences",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    MarketingOptIn = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    SenderId = table.Column<string>(type: "text", nullable: false),
                    SenderName = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Internal = table.Column<bool>(type: "boolean", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    ContextType = table.Column<string>(type: "text", nullable: false),
                    ContextId = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Credits",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    ClaimId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryAssignments",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RouteId = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PickedUpAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArrivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryRoutes",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    VehicleReference = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "numeric", nullable: false),
                    CashState = table.Column<string>(type: "text", nullable: false),
                    DeclaredAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    CountedAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpectedAtDeclaration = table.Column<decimal>(type: "numeric", nullable: true),
                    Shortage = table.Column<decimal>(type: "numeric", nullable: true),
                    Surplus = table.Column<decimal>(type: "numeric", nullable: true),
                    DeclarationNote = table.Column<string>(type: "text", nullable: false),
                    FinanceNote = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CountedBy = table.Column<string>(type: "text", nullable: true),
                    ReconciledBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DepartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeclaredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CountedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReconciledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemoGuests",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InvitationId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    JoinKeyHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoGuests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemoInvitations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    MaxUses = table.Column<int>(type: "integer", nullable: false),
                    Uses = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DriverLocations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Accuracy = table.Column<double>(type: "double precision", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Consent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EngagementNotices",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    AggregateId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngagementNotices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiftRequests",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    RewardId = table.Column<string>(type: "text", nullable: false),
                    RewardName = table.Column<string>(type: "text", nullable: false),
                    GrantId = table.Column<string>(type: "text", nullable: false),
                    PointsCost = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProofReference = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FulfilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inventory",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Delta = table.Column<int>(type: "integer", nullable: false),
                    ReservedDelta = table.Column<int>(type: "integer", nullable: false),
                    QuarantineDelta = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyEntries",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    SourceRef = table.Column<string>(type: "text", nullable: false),
                    AvailableDelta = table.Column<int>(type: "integer", nullable: false),
                    ReservedDelta = table.Column<int>(type: "integer", nullable: false),
                    SpentDelta = table.Column<int>(type: "integer", nullable: false),
                    CorrectionDueDelta = table.Column<int>(type: "integer", nullable: false),
                    OrderPointDelta = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyWallets",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    Available = table.Column<int>(type: "integer", nullable: false),
                    Reserved = table.Column<int>(type: "integer", nullable: false),
                    Spent = table.Column<int>(type: "integer", nullable: false),
                    CorrectionDue = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyWallets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Offers",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: true),
                    OnHand = table.Column<int>(type: "integer", nullable: true),
                    Reserved = table.Column<int>(type: "integer", nullable: false),
                    Quarantined = table.Column<int>(type: "integer", nullable: false),
                    CaseSize = table.Column<int>(type: "integer", nullable: false),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                    OfferApproved = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Offers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictScopes",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationReceipts",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    ChallengeId = table.Column<string>(type: "text", nullable: false),
                    AcceptedVersion = table.Column<int>(type: "integer", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderEvents",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    AddressJson = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    AdminNote = table.Column<string>(type: "text", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric", nullable: true),
                    DeliveryFee = table.Column<decimal>(type: "numeric", nullable: true),
                    Total = table.Column<decimal>(type: "numeric", nullable: true),
                    Collected = table.Column<decimal>(type: "numeric", nullable: false),
                    QuoteVersion = table.Column<int>(type: "integer", nullable: false),
                    QuoteValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReservationHeld = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    IsRefund = table.Column<bool>(type: "boolean", nullable: false),
                    Reconciled = table.Column<bool>(type: "boolean", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IsCatalogActive = table.Column<bool>(type: "boolean", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceLicense = table.Column<string>(type: "text", nullable: false),
                    ImageLicense = table.Column<string>(type: "text", nullable: false),
                    CountryTags = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionEvents",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    PromotionSnapshotId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    DiscountMinor = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionSnapshots",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    QuoteVersion = table.Column<int>(type: "integer", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    CampaignVersion = table.Column<int>(type: "integer", nullable: false),
                    CampaignTitle = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    QuotedQuantity = table.Column<int>(type: "integer", nullable: false),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                    BaseUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    QuotedDiscountMinor = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveDiscountMinor = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedTriggerQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReturnedTriggerQuantity = table.Column<int>(type: "integer", nullable: false),
                    GiftProductId = table.Column<string>(type: "text", nullable: false),
                    GiftProductName = table.Column<string>(type: "text", nullable: false),
                    GiftQuantity = table.Column<int>(type: "integer", nullable: false),
                    GiftStockState = table.Column<string>(type: "text", nullable: false),
                    GiftAcceptedQuantity = table.Column<int>(type: "integer", nullable: false),
                    GiftRefusedQuantity = table.Column<int>(type: "integer", nullable: false),
                    GiftProofReference = table.Column<string>(type: "text", nullable: false),
                    EligibilityReviewRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptChallenges",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RouteId = table.Column<string>(type: "text", nullable: false),
                    RouteVersion = table.Column<int>(type: "integer", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ItemsHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RewardItems",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    PointsCost = table.Column<int>(type: "integer", nullable: false),
                    AvailableStock = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RouteCustodyChallenges",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    RouteId = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    WarehouseActorId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ManifestHash = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteCustodyChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RouteCustodyReceipts",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    RouteId = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    WarehouseActorId = table.Column<string>(type: "text", nullable: false),
                    AcceptedVersion = table.Column<int>(type: "integer", nullable: false),
                    ManifestJson = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteCustodyReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OwnerCapability = table.Column<bool>(type: "boolean", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Contact = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplyInspections",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ReceiptLineId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Disposition = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplyOrderLines",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    SupplyOrderId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    OrderedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false),
                    QuarantineQuantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCostMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyOrderLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplyOrders",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvoiceReference = table.Column<string>(type: "text", nullable: false),
                    InvoiceMinor = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceMatchStatus = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplyReceiptLines",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ReceiptId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false),
                    QuarantineQuantity = table.Column<int>(type: "integer", nullable: false),
                    InspectedQuantity = table.Column<int>(type: "integer", nullable: false),
                    LotNumber = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyReceiptLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplyReceipts",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    SupplyOrderId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsDemo = table.Column<bool>(type: "boolean", nullable: false),
                    AccessHash = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~",
                        column: x => x.ApplicationId,
                        principalSchema: Schema,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderLine",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PreparedQuantity = table.Column<int>(type: "integer", nullable: false),
                    PurchaseOrderId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    RequestedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    CaseSize = table.Column<int>(type: "integer", nullable: false),
                    AcceptedQuantity = table.Column<int>(type: "integer", nullable: false),
                    RefusedQuantity = table.Column<int>(type: "integer", nullable: false),
                    InspectedQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLine_Orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: Schema,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    AuthorizationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: Schema,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalSchema: Schema,
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                schema: Schema,
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Audit_WorkspaceId",
                schema: Schema,
                table: "Audit",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_BroadcastId_MerchantId",
                schema: Schema,
                table: "BroadcastRecipients",
                columns: new[] { "BroadcastId", "MerchantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_WorkspaceId_IdempotencyKey",
                schema: Schema,
                table: "Broadcasts",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignGrants_CampaignId_MerchantId",
                schema: Schema,
                table: "CampaignGrants",
                columns: new[] { "CampaignId", "MerchantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_AccountId_ProductId",
                schema: Schema,
                table: "CartItems",
                columns: new[] { "AccountId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashReceipts_WorkspaceId_OrderId_IdempotencyKey",
                schema: Schema,
                table: "CashReceipts",
                columns: new[] { "WorkspaceId", "OrderId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_WorkspaceId_OrderId",
                schema: Schema,
                table: "Claims",
                columns: new[] { "WorkspaceId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationPreferences_WorkspaceId_MerchantId",
                schema: Schema,
                table: "CommunicationPreferences",
                columns: new[] { "WorkspaceId", "MerchantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId_SenderId_IdempotencyKey",
                schema: Schema,
                table: "ConversationMessages",
                columns: new[] { "ConversationId", "SenderId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_WorkspaceId_MerchantId_ContextType_ContextId",
                schema: Schema,
                table: "Conversations",
                columns: new[] { "WorkspaceId", "MerchantId", "ContextType", "ContextId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Credits_WorkspaceId_ClaimId_IdempotencyKey",
                schema: Schema,
                table: "Credits",
                columns: new[] { "WorkspaceId", "ClaimId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_WorkspaceId_DriverId",
                schema: Schema,
                table: "DeliveryAssignments",
                columns: new[] { "WorkspaceId", "DriverId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_WorkspaceId_OrderId",
                schema: Schema,
                table: "DeliveryAssignments",
                columns: new[] { "WorkspaceId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRoutes_WorkspaceId_DriverId",
                schema: Schema,
                table: "DeliveryRoutes",
                columns: new[] { "WorkspaceId", "DriverId" });

            migrationBuilder.CreateIndex(
                name: "IX_DemoGuests_AccountId",
                schema: Schema,
                table: "DemoGuests",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoGuests_InvitationId_JoinKeyHash",
                schema: Schema,
                table: "DemoGuests",
                columns: new[] { "InvitationId", "JoinKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoInvitations_TokenHash",
                schema: Schema,
                table: "DemoInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoInvitations_WorkspaceId",
                schema: Schema,
                table: "DemoInvitations",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverLocations_WorkspaceId_DriverId",
                schema: Schema,
                table: "DriverLocations",
                columns: new[] { "WorkspaceId", "DriverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_AccountId_ProductId",
                schema: Schema,
                table: "Favorites",
                columns: new[] { "AccountId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftRequests_WorkspaceId_MerchantId_IdempotencyKey",
                schema: Schema,
                table: "GiftRequests",
                columns: new[] { "WorkspaceId", "MerchantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyEntries_WorkspaceId_MerchantId_SourceRef",
                schema: Schema,
                table: "LoyaltyEntries",
                columns: new[] { "WorkspaceId", "MerchantId", "SourceRef" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyWallets_WorkspaceId_MerchantId",
                schema: Schema,
                table: "LoyaltyWallets",
                columns: new[] { "WorkspaceId", "MerchantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Offers_WorkspaceId_ProductId",
                schema: Schema,
                table: "Offers",
                columns: new[] { "WorkspaceId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                schema: Schema,
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                schema: Schema,
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                schema: Schema,
                table: "OpenIddictScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                schema: Schema,
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                schema: Schema,
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                schema: Schema,
                table: "OpenIddictTokens",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationReceipts_WorkspaceId_OrderId",
                schema: Schema,
                table: "OperationReceipts",
                columns: new[] { "WorkspaceId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_OrderId",
                schema: Schema,
                table: "OrderEvents",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLine_PurchaseOrderId",
                schema: Schema,
                table: "OrderLine",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_WorkspaceId_AccountId_IdempotencyKey",
                schema: Schema,
                table: "Orders",
                columns: new[] { "WorkspaceId", "AccountId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_WorkspaceId_OrderId_IdempotencyKey",
                schema: Schema,
                table: "Payments",
                columns: new[] { "WorkspaceId", "OrderId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                schema: Schema,
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionEvents_PromotionSnapshotId_IdempotencyKey",
                schema: Schema,
                table: "PromotionEvents",
                columns: new[] { "PromotionSnapshotId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionSnapshots_OrderId_QuoteVersion_ProductId",
                schema: Schema,
                table: "PromotionSnapshots",
                columns: new[] { "OrderId", "QuoteVersion", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptChallenges_TokenHash",
                schema: Schema,
                table: "ReceiptChallenges",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteCustodyChallenges_TokenHash",
                schema: Schema,
                table: "RouteCustodyChallenges",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteCustodyReceipts_WorkspaceId_RouteId",
                schema: Schema,
                table: "RouteCustodyReceipts",
                columns: new[] { "WorkspaceId", "RouteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TokenHash",
                schema: Schema,
                table: "Sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplyInspections_ReceiptLineId_IdempotencyKey",
                schema: Schema,
                table: "SupplyInspections",
                columns: new[] { "ReceiptLineId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplyOrderLines_SupplyOrderId_ProductId",
                schema: Schema,
                table: "SupplyOrderLines",
                columns: new[] { "SupplyOrderId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplyOrders_WorkspaceId_IdempotencyKey",
                schema: Schema,
                table: "SupplyOrders",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplyReceipts_SupplyOrderId_IdempotencyKey",
                schema: Schema,
                table: "SupplyReceipts",
                columns: new[] { "SupplyOrderId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Addresses",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Audit",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "BroadcastRecipients",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Broadcasts",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "CampaignGrants",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Campaigns",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "CartItems",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "CashReceipts",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Claims",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "CommunicationPreferences",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "ConversationMessages",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Credits",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "DeliveryAssignments",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "DeliveryRoutes",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "DemoGuests",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "DemoInvitations",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "DriverLocations",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "EngagementNotices",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Favorites",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "GiftRequests",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Inventory",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "LoyaltyEntries",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "LoyaltyWallets",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Offers",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OpenIddictScopes",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OpenIddictTokens",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OperationReceipts",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OrderEvents",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OrderLine",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Payments",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Products",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "PromotionEvents",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "PromotionSnapshots",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "ReceiptChallenges",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "RewardItems",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "RouteCustodyChallenges",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "RouteCustodyReceipts",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "SupplyInspections",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "SupplyOrderLines",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "SupplyOrders",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "SupplyReceiptLines",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "SupplyReceipts",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Workspaces",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "Orders",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OpenIddictApplications",
                schema: Schema);
        }
    }
}
