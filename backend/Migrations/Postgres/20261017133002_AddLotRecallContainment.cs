using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddLotRecallContainment : Migration, Toushir.Wholesale.ISchemaMigration
    {
        /// <inheritdoc />
        public string Schema { get; set; } = "commerce_ecosystem";
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecallCases",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    LotId = table.Column<string>(type: "text", nullable: false),
                    IdentityKey = table.Column<string>(type: "text", nullable: false),
                    LotNumber = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    SafetyInstruction = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ContainmentActive = table.Column<bool>(type: "boolean", nullable: false),
                    UnknownUntrackedOrderCount = table.Column<int>(type: "integer", nullable: false),
                    UnknownUntrackedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedBy = table.Column<string>(type: "text", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosureNote = table.Column<string>(type: "text", nullable: false),
                    UnknownScopeReason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecallEvents",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    RecallId = table.Column<string>(type: "text", nullable: false),
                    PositionId = table.Column<string>(type: "text", nullable: false),
                    WarehouseLotId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Destination = table.Column<string>(type: "text", nullable: false),
                    ProofReference = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecallNotices",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    RecallId = table.Column<string>(type: "text", nullable: false),
                    RecipientId = table.Column<string>(type: "text", nullable: false),
                    RecipientRole = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedBy = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallNotices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecallOperations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    RecallId = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecallPositions",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    RecallId = table.Column<string>(type: "text", nullable: false),
                    PositionKey = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    LotId = table.Column<string>(type: "text", nullable: false),
                    AllocationId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    DriverId = table.Column<string>(type: "text", nullable: false),
                    SnapshotOrderStatus = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RecoveredQuantity = table.Column<int>(type: "integer", nullable: false),
                    ExceptionQuantity = table.Column<int>(type: "integer", nullable: false),
                    RequiresRecovery = table.Column<bool>(type: "boolean", nullable: false),
                    ExceptionReason = table.Column<string>(type: "text", nullable: false),
                    ExceptionReference = table.Column<string>(type: "text", nullable: false),
                    ExceptionBy = table.Column<string>(type: "text", nullable: false),
                    ExceptionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallPositions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecallCases_WorkspaceId_IdentityKey",
                schema: Schema,
                table: "RecallCases",
                columns: new[] { "WorkspaceId", "IdentityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecallEvents_WorkspaceId_RecallId",
                schema: Schema,
                table: "RecallEvents",
                columns: new[] { "WorkspaceId", "RecallId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecallNotices_RecallId_RecipientRole_RecipientId",
                schema: Schema,
                table: "RecallNotices",
                columns: new[] { "RecallId", "RecipientRole", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecallOperations_WorkspaceId_ActorId_IdempotencyKey",
                schema: Schema,
                table: "RecallOperations",
                columns: new[] { "WorkspaceId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecallPositions_RecallId_PositionKey",
                schema: Schema,
                table: "RecallPositions",
                columns: new[] { "RecallId", "PositionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecallCases",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "RecallEvents",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "RecallNotices",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "RecallOperations",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "RecallPositions",
                schema: Schema);
        }
    }
}
