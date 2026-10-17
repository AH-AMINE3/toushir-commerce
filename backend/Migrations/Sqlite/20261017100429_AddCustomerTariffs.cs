using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddCustomerTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantPriceSegments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    MerchantId = table.Column<string>(type: "TEXT", nullable: false),
                    SegmentId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantPriceSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderPricingSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    OrderId = table.Column<string>(type: "TEXT", nullable: false),
                    QuoteVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    MerchantId = table.Column<string>(type: "TEXT", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    ResolutionSource = table.Column<string>(type: "TEXT", nullable: false),
                    RuleId = table.Column<string>(type: "TEXT", nullable: false),
                    RuleVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedUnitPriceMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    BaseUnitPriceMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    AppliedUnitPriceMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    CampaignsAllowed = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverrideReason = table.Column<string>(type: "TEXT", nullable: false),
                    TermsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPricingSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceSegments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    AggregateId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UtcTicks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TariffRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", nullable: false),
                    MinimumQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    UnitPriceMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AllowCampaigns = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewReason = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    WithdrawnBy = table.Column<string>(type: "TEXT", nullable: false),
                    WithdrawalReason = table.Column<string>(type: "TEXT", nullable: false),
                    SupersedesId = table.Column<string>(type: "TEXT", nullable: false),
                    SupersedesVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantPriceSegments_WorkspaceId_MerchantId",
                table: "MerchantPriceSegments",
                columns: new[] { "WorkspaceId", "MerchantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPricingSnapshots_WorkspaceId_OrderId_QuoteVersion_ProductId",
                table: "OrderPricingSnapshots",
                columns: new[] { "WorkspaceId", "OrderId", "QuoteVersion", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceSegments_WorkspaceId_NormalizedName",
                table: "PriceSegments",
                columns: new[] { "WorkspaceId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingEvents_WorkspaceId_AggregateId",
                table: "PricingEvents",
                columns: new[] { "WorkspaceId", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingEvents_WorkspaceId_UtcTicks",
                table: "PricingEvents",
                columns: new[] { "WorkspaceId", "UtcTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_TariffRules_WorkspaceId_IdempotencyKey",
                table: "TariffRules",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TariffRules_WorkspaceId_ProductId_Status",
                table: "TariffRules",
                columns: new[] { "WorkspaceId", "ProductId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantPriceSegments");

            migrationBuilder.DropTable(
                name: "OrderPricingSnapshots");

            migrationBuilder.DropTable(
                name: "PriceSegments");

            migrationBuilder.DropTable(
                name: "PricingEvents");

            migrationBuilder.DropTable(
                name: "TariffRules");
        }
    }
}
