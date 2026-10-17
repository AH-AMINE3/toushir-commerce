using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddCampaignAudienceAndOrderAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignAudiences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<string>(type: "TEXT", nullable: false),
                    MerchantId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAudiences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderAmendments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    OrderId = table.Column<string>(type: "TEXT", nullable: false),
                    MerchantId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseOrderVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ProposerName = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", nullable: false),
                    LinesJson = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: false),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: false),
                    TermsHash = table.Column<string>(type: "TEXT", nullable: false),
                    DecidedBy = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionName = table.Column<string>(type: "TEXT", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DecisionReason = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionKey = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionRequestHash = table.Column<string>(type: "TEXT", nullable: false),
                    AcceptedOrderVersion = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAmendments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAudiences_WorkspaceId_CampaignId_MerchantId",
                table: "CampaignAudiences",
                columns: new[] { "WorkspaceId", "CampaignId", "MerchantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderAmendments_WorkspaceId_MerchantId",
                table: "OrderAmendments",
                columns: new[] { "WorkspaceId", "MerchantId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderAmendments_WorkspaceId_OrderId",
                table: "OrderAmendments",
                columns: new[] { "WorkspaceId", "OrderId" },
                unique: true,
                filter: "\"Status\" = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAmendments_WorkspaceId_OrderId_IdempotencyKey",
                table: "OrderAmendments",
                columns: new[] { "WorkspaceId", "OrderId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignAudiences");

            migrationBuilder.DropTable(
                name: "OrderAmendments");
        }
    }
}
