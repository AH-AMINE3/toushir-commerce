using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toushir.Wholesale.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddRouteCapacityAndStopOrder : Migration,Toushir.Wholesale.ISchemaMigration
    {
        public string Schema {get;set;}="commerce_ecosystem";
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CapacityUnits",
                schema: Schema,
                table: "DeliveryRoutes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StopRank",
                schema: Schema,
                table: "DeliveryAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapacityUnits",
                schema: Schema,
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "StopRank",
                schema: Schema,
                table: "DeliveryAssignments");
        }
    }
}
