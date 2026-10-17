using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddShopMemberships : Migration, Toushir.Wholesale.ISchemaMigration
    {
        public string Schema { get; set; } = "commerce_ecosystem";
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssuedBy",
                schema: Schema,
                table: "ReceiptChallenges",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: Schema,
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                schema: Schema,
                table: "OrderEvents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ShopAccessEvents",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UtcTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopAccessEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopMemberInvitations",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    AcceptedBy = table.Column<string>(type: "text", nullable: false),
                    RevokedBy = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopMemberInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopMemberships",
                schema: Schema,
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkspaceId = table.Column<string>(type: "text", nullable: false),
                    ShopId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    InvitationId = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopMemberships", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopAccessEvents_WorkspaceId_ShopId_UtcTicks",
                schema: Schema,
                table: "ShopAccessEvents",
                columns: new[] { "WorkspaceId", "ShopId", "UtcTicks" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopMemberInvitations_TokenHash",
                schema: Schema,
                table: "ShopMemberInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopMemberInvitations_WorkspaceId_ShopId",
                schema: Schema,
                table: "ShopMemberInvitations",
                columns: new[] { "WorkspaceId", "ShopId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopMemberships_AccountId",
                schema: Schema,
                table: "ShopMemberships",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopMemberships_WorkspaceId_ShopId",
                schema: Schema,
                table: "ShopMemberships",
                columns: new[] { "WorkspaceId", "ShopId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopAccessEvents",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "ShopMemberInvitations",
                schema: Schema);

            migrationBuilder.DropTable(
                name: "ShopMemberships",
                schema: Schema);

            migrationBuilder.DropColumn(
                name: "IssuedBy",
                schema: Schema,
                table: "ReceiptChallenges");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: Schema,
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: Schema,
                table: "OrderEvents");
        }
    }
}
