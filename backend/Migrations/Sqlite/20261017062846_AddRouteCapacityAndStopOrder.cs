using Microsoft.EntityFrameworkCore.Migrations;
namespace Toushir.Wholesale.Migrations.Sqlite;
public partial class AddRouteCapacityAndStopOrder : Migration {
 protected override void Up(MigrationBuilder migrationBuilder){migrationBuilder.AddColumn<int>(name:"CapacityUnits",table:"DeliveryRoutes",type:"INTEGER",nullable:true);migrationBuilder.AddColumn<int>(name:"StopRank",table:"DeliveryAssignments",type:"INTEGER",nullable:false,defaultValue:0);}
 protected override void Down(MigrationBuilder migrationBuilder){migrationBuilder.DropColumn(name:"CapacityUnits",table:"DeliveryRoutes");migrationBuilder.DropColumn(name:"StopRank",table:"DeliveryAssignments");}
}
