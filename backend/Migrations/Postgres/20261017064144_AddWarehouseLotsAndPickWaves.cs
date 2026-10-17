using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddWarehouseLotsAndPickWaves : Migration,Toushir.Wholesale.ISchemaMigration
    {
        /// <inheritdoc />
        public string Schema {get;set;}="commerce_ecosystem";
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LotAllocations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<string>(type: "text", nullable: false),
                    OrderLineId = table.Column<string>(type: "text", nullable: false),
                    PromotionSnapshotId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    LotId = table.Column<string>(type: "text", nullable: false),
                    AllocationKey = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PickedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    WaveId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LotProductControls",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    ActivatedBy = table.Column<string>(type: "text", nullable: false),
                    CountReference = table.Column<string>(type: "text", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotProductControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickWaves",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickWaves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseLots",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    LotNumber = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OnHand = table.Column<int>(type: "integer", nullable: false),
                    Reserved = table.Column<int>(type: "integer", nullable: false),
                    Quarantined = table.Column<int>(type: "integer", nullable: false),
                    InitialQuantity = table.Column<int>(type: "integer", nullable: false),
                    SourceKey = table.Column<string>(type: "text", nullable: false),
                    SourceRef = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseLots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseOperations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ResultId = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotAllocations_OrderId_AllocationKey_LotId",
                schema: Schema,
                table: "LotAllocations",
                columns: new[] { "OrderId", "AllocationKey", "LotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotProductControls_WorkspaceId_ProductId",
                schema: Schema,
                table: "LotProductControls",
                columns: new[] { "WorkspaceId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLots_WorkspaceId_SourceKey",
                schema: Schema,
                table: "WarehouseLots",
                columns: new[] { "WorkspaceId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseOperations_WorkspaceId_ActorId_IdempotencyKey",
                schema: Schema,
                table: "WarehouseOperations",
                columns: new[] { "WorkspaceId", "ActorId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotAllocations",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "LotProductControls",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "PickWaves",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "WarehouseLots",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "WarehouseOperations",
                schema: Schema);
        }
    }
}
