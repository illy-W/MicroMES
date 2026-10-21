using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBomConsumedInStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsumedInStepId",
                table: "BomItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_ConsumedInStepId",
                table: "BomItems",
                column: "ConsumedInStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_BomItems_RoutingSteps_ConsumedInStepId",
                table: "BomItems",
                column: "ConsumedInStepId",
                principalTable: "RoutingSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BomItems_RoutingSteps_ConsumedInStepId",
                table: "BomItems");

            migrationBuilder.DropIndex(
                name: "IX_BomItems_ConsumedInStepId",
                table: "BomItems");

            migrationBuilder.DropColumn(
                name: "ConsumedInStepId",
                table: "BomItems");
        }
    }
}
