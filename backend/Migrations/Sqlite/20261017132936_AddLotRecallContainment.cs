using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddLotRecallContainment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecallCases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    LotId = table.Column<string>(type: "TEXT", nullable: false),
                    IdentityKey = table.Column<string>(type: "TEXT", nullable: false),
                    LotNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    SafetyInstruction = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ContainmentActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    UnknownUntrackedOrderCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UnknownUntrackedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ClosureNote = table.Column<string>(type: "TEXT", nullable: false),
                    UnknownScopeReason = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecallEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    RecallId = table.Column<string>(type: "TEXT", nullable: false),
                    PositionId = table.Column<string>(type: "TEXT", nullable: false),
                    WarehouseLotId = table.Column<string>(type: "TEXT", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Destination = table.Column<string>(type: "TEXT", nullable: false),
                    ProofReference = table.Column<string>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecallNotices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    RecallId = table.Column<string>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<string>(type: "TEXT", nullable: false),
                    RecipientRole = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReceivedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallNotices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecallOperations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", nullable: false),
                    RecallId = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecallPositions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    RecallId = table.Column<string>(type: "TEXT", nullable: false),
                    PositionKey = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    LotId = table.Column<string>(type: "TEXT", nullable: false),
                    AllocationId = table.Column<string>(type: "TEXT", nullable: false),
                    OrderId = table.Column<string>(type: "TEXT", nullable: false),
                    ShopId = table.Column<string>(type: "TEXT", nullable: false),
                    DriverId = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotOrderStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RecoveredQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ExceptionQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresRecovery = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExceptionReason = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionReference = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionBy = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecallPositions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecallCases_WorkspaceId_IdentityKey",
                table: "RecallCases",
                columns: new[] { "WorkspaceId", "IdentityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecallEvents_WorkspaceId_RecallId",
                table: "RecallEvents",
                columns: new[] { "WorkspaceId", "RecallId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecallNotices_RecallId_RecipientRole_RecipientId",
                table: "RecallNotices",
                columns: new[] { "RecallId", "RecipientRole", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecallOperations_WorkspaceId_ActorId_IdempotencyKey",
                table: "RecallOperations",
                columns: new[] { "WorkspaceId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecallPositions_RecallId_PositionKey",
                table: "RecallPositions",
                columns: new[] { "RecallId", "PositionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecallCases");

            migrationBuilder.DropTable(
                name: "RecallEvents");

            migrationBuilder.DropTable(
                name: "RecallNotices");

            migrationBuilder.DropTable(
                name: "RecallOperations");

            migrationBuilder.DropTable(
                name: "RecallPositions");
        }
    }
}
