using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddWarehouseLotsAndPickWaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotAllocations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    OrderId = table.Column<string>(type: "TEXT", nullable: false),
                    OrderLineId = table.Column<string>(type: "TEXT", nullable: false),
                    PromotionSnapshotId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    LotId = table.Column<string>(type: "TEXT", nullable: false),
                    AllocationKey = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    PickedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    WaveId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LotProductControls",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ActivatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CountReference = table.Column<string>(type: "TEXT", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotProductControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickWaves",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickWaves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseLots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    LotNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    OnHand = table.Column<int>(type: "INTEGER", nullable: false),
                    Reserved = table.Column<int>(type: "INTEGER", nullable: false),
                    Quarantined = table.Column<int>(type: "INTEGER", nullable: false),
                    InitialQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRef = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseLots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseOperations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<string>(type: "TEXT", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    ResultId = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotAllocations_OrderId_AllocationKey_LotId",
                table: "LotAllocations",
                columns: new[] { "OrderId", "AllocationKey", "LotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotProductControls_WorkspaceId_ProductId",
                table: "LotProductControls",
                columns: new[] { "WorkspaceId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLots_WorkspaceId_SourceKey",
                table: "WarehouseLots",
                columns: new[] { "WorkspaceId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseOperations_WorkspaceId_ActorId_IdempotencyKey",
                table: "WarehouseOperations",
                columns: new[] { "WorkspaceId", "ActorId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotAllocations");

            migrationBuilder.DropTable(
                name: "LotProductControls");

            migrationBuilder.DropTable(
                name: "PickWaves");

            migrationBuilder.DropTable(
                name: "WarehouseLots");

            migrationBuilder.DropTable(
                name: "WarehouseOperations");
        }
    }
}
