using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddCustomerTariffs : Migration, Toushir.Wholesale.ISchemaMigration
    {
        public string Schema { get; set; } = "commerce_ecosystem";
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantPriceSegments",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    SegmentId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantPriceSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderPricingSnapshots",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    QuoteVersion = table.Column<int>(type: "integer", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ResolutionSource = table.Column<string>(type: "text", nullable: false),
                    RuleId = table.Column<string>(type: "text", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    ExpectedUnitPriceMinor = table.Column<long>(type: "bigint", nullable: true),
                    BaseUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    AppliedUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    CampaignsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    OverrideReason = table.Column<string>(type: "text", nullable: false),
                    TermsJson = table.Column<string>(type: "text", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPricingSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceSegments",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingEvents",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    AggregateId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UtcTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TariffRules",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<string>(type: "text", nullable: false),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                    MaximumQuantity = table.Column<int>(type: "integer", nullable: true),
                    UnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AllowCampaigns = table.Column<bool>(type: "boolean", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedBy = table.Column<string>(type: "text", nullable: false),
                    ReviewReason = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnBy = table.Column<string>(type: "text", nullable: false),
                    WithdrawalReason = table.Column<string>(type: "text", nullable: false),
                    SupersedesId = table.Column<string>(type: "text", nullable: false),
                    SupersedesVersion = table.Column<int>(type: "integer", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantPriceSegments_WorkspaceId_MerchantId",
                schema: Schema,
                table: "MerchantPriceSegments",
                columns: new[] { "WorkspaceId", "MerchantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingSnapshots_WorkspaceId_OrderId_QuoteVersion_Prod~",
                schema: Schema,
                table: "OrderPricingSnapshots",
                columns: new[] { "WorkspaceId", "OrderId", "QuoteVersion", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceSegments_WorkspaceId_NormalizedName",
                schema: Schema,
                table: "PriceSegments",
                columns: new[] { "WorkspaceId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingEvents_WorkspaceId_AggregateId",
                schema: Schema,
                table: "PricingEvents",
                columns: new[] { "WorkspaceId", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingEvents_WorkspaceId_UtcTicks",
                schema: Schema,
                table: "PricingEvents",
                columns: new[] { "WorkspaceId", "UtcTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_TariffRules_WorkspaceId_IdempotencyKey",
                schema: Schema,
                table: "TariffRules",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TariffRules_WorkspaceId_ProductId_Status",
                schema: Schema,
                table: "TariffRules",
                columns: new[] { "WorkspaceId", "ProductId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantPriceSegments",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OrderPricingSnapshots",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "PriceSegments",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "PricingEvents",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "TariffRules",
                schema: Schema);
        }
    }
}
