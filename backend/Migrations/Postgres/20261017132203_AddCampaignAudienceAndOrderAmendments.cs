using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddCampaignAudienceAndOrderAmendments : Migration, Toushir.Wholesale.ISchemaMigration
    {
        /// <inheritdoc />
        public string Schema { get; set; } = "commerce_ecosystem";
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignAudiences",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAudiences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderAmendments",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    BaseOrderVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ProposedBy = table.Column<string>(type: "text", nullable: false),
                    ProposerName = table.Column<string>(type: "text", nullable: false),
                    ProposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    LinesJson = table.Column<string>(type: "text", nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false),
                    TermsHash = table.Column<string>(type: "text", nullable: false),
                    DecidedBy = table.Column<string>(type: "text", nullable: false),
                    DecisionName = table.Column<string>(type: "text", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "text", nullable: false),
                    DecisionKey = table.Column<string>(type: "text", nullable: false),
                    DecisionRequestHash = table.Column<string>(type: "text", nullable: false),
                    AcceptedOrderVersion = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAmendments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAudiences_WorkspaceId_CampaignId_MerchantId",
                schema: Schema,
                table: "CampaignAudiences",
                columns: new[] { "WorkspaceId", "CampaignId", "MerchantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderAmendments_WorkspaceId_MerchantId",
                schema: Schema,
                table: "OrderAmendments",
                columns: new[] { "WorkspaceId", "MerchantId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderAmendments_WorkspaceId_OrderId",
                schema: Schema,
                table: "OrderAmendments",
                columns: new[] { "WorkspaceId", "OrderId" },
                unique: true,
                filter: "\"Status\" = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAmendments_WorkspaceId_OrderId_IdempotencyKey",
                schema: Schema,
                table: "OrderAmendments",
                columns: new[] { "WorkspaceId", "OrderId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignAudiences",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "OrderAmendments",
                schema: Schema);
        }
    }
}
