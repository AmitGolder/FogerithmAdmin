using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fogerithm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActualPartToProductionConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActualPartId",
                table: "ProductionConsumptions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionConsumptions_ActualPartId",
                table: "ProductionConsumptions",
                column: "ActualPartId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionConsumptions_Parts_ActualPartId",
                table: "ProductionConsumptions",
                column: "ActualPartId",
                principalTable: "Parts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionConsumptions_Parts_ActualPartId",
                table: "ProductionConsumptions");

            migrationBuilder.DropIndex(
                name: "IX_ProductionConsumptions_ActualPartId",
                table: "ProductionConsumptions");

            migrationBuilder.DropColumn(
                name: "ActualPartId",
                table: "ProductionConsumptions");
        }
    }
}
