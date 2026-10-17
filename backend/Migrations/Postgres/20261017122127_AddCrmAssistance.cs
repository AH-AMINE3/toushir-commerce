using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddCrmAssistance : Migration, Toushir.Wholesale.ISchemaMigration
    {
        public string Schema { get; set; } = "commerce_ecosystem";
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmAssistances",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    AdvisorId = table.Column<string>(type: "text", nullable: false),
                    PortfolioVersion = table.Column<int>(type: "integer", nullable: false),
                    IssuedBy = table.Column<string>(type: "text", nullable: false),
                    IssuerRole = table.Column<string>(type: "text", nullable: false),
                    IssuerMembershipVersion = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<string>(type: "text", nullable: false),
                    AddressJson = table.Column<string>(type: "text", nullable: false),
                    LinesJson = table.Column<string>(type: "text", nullable: false),
                    ProductSnapshotsJson = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<string>(type: "text", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsedBy = table.Column<string>(type: "text", nullable: false),
                    UseKeyHash = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmAssistances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmContacts",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    NextAction = table.Column<string>(type: "text", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmEvents",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UtcTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmPortfolios",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    AdvisorId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmPortfolios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmTasks",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VisitEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedBy = table.Column<string>(type: "text", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmAssistances_OrderId",
                schema: Schema,
                table: "CrmAssistances",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmAssistances_WorkspaceId_ShopId",
                schema: Schema,
                table: "CrmAssistances",
                columns: new[] { "WorkspaceId", "ShopId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmContacts_WorkspaceId_ShopId",
                schema: Schema,
                table: "CrmContacts",
                columns: new[] { "WorkspaceId", "ShopId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmEvents_WorkspaceId_ShopId_UtcTicks",
                schema: Schema,
                table: "CrmEvents",
                columns: new[] { "WorkspaceId", "ShopId", "UtcTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmPortfolios_WorkspaceId_ShopId",
                schema: Schema,
                table: "CrmPortfolios",
                columns: new[] { "WorkspaceId", "ShopId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmTasks_WorkspaceId_ShopId",
                schema: Schema,
                table: "CrmTasks",
                columns: new[] { "WorkspaceId", "ShopId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmAssistances",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "CrmContacts",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "CrmEvents",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "CrmPortfolios",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "CrmTasks",
                schema: Schema);
        }
    }
}
