using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddSupplierDocumentsAndConfirmations : Migration, Toushir.Wholesale.ISchemaMigration
    {
        public string Schema { get; set; } = "commerce_ecosystem";
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierConfirmations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    SupplyOrderId = table.Column<string>(type: "text", nullable: false),
                    DocumentId = table.Column<string>(type: "text", nullable: false),
                    SupplierReference = table.Column<string>(type: "text", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    OriginalTermsHash = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    ReviewedBy = table.Column<string>(type: "text", nullable: false),
                    ReviewReason = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ExpectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierConfirmations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDocuments",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    SupplyOrderId = table.Column<string>(type: "text", nullable: false),
                    ReceiptId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    ByteLength = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SupersedesId = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    VoidReason = table.Column<string>(type: "text", nullable: false),
                    VoidedBy = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DocumentDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplyProcurementEvents",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    SupplyOrderId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ReferenceId = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyProcurementEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierConfirmations_WorkspaceId_IdempotencyKey",
                schema: Schema,
                table: "SupplierConfirmations",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierConfirmations_WorkspaceId_SupplyOrderId",
                schema: Schema,
                table: "SupplierConfirmations",
                columns: new[] { "WorkspaceId", "SupplyOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocuments_WorkspaceId_IdempotencyKey",
                schema: Schema,
                table: "SupplierDocuments",
                columns: new[] { "WorkspaceId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocuments_WorkspaceId_SupplyOrderId",
                schema: Schema,
                table: "SupplierDocuments",
                columns: new[] { "WorkspaceId", "SupplyOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyProcurementEvents_WorkspaceId_SupplyOrderId",
                schema: Schema,
                table: "SupplyProcurementEvents",
                columns: new[] { "WorkspaceId", "SupplyOrderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierConfirmations",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "SupplierDocuments",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "SupplyProcurementEvents",
                schema: Schema);
        }
    }
}
