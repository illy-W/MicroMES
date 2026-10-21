using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EquipmentProcessSegments_ProcessSegmentId",
                table: "EquipmentProcessSegments");

            migrationBuilder.AddColumn<int>(
                name: "AssignedEquipmentId",
                table: "WorkOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentCode",
                table: "WorkOrderResponses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EquipmentId",
                table: "WorkOrderResponses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentName",
                table: "WorkOrderResponses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewEquipmentCode",
                table: "WorkOrderResponseCorrections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewEquipmentName",
                table: "WorkOrderResponseCorrections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousEquipmentCode",
                table: "WorkOrderResponseCorrections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousEquipmentName",
                table: "WorkOrderResponseCorrections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "EquipmentProcessSegments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssignedEquipmentId",
                table: "WorkOrders",
                column: "AssignedEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderResponses_EquipmentId",
                table: "WorkOrderResponses",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentProcessSegments_ProcessSegmentId_Default",
                table: "EquipmentProcessSegments",
                column: "ProcessSegmentId",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderResponses_Equipment_EquipmentId",
                table: "WorkOrderResponses",
                column: "EquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Equipment_AssignedEquipmentId",
                table: "WorkOrders",
                column: "AssignedEquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderResponses_Equipment_EquipmentId",
                table: "WorkOrderResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Equipment_AssignedEquipmentId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_AssignedEquipmentId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderResponses_EquipmentId",
                table: "WorkOrderResponses");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentProcessSegments_ProcessSegmentId_Default",
                table: "EquipmentProcessSegments");

            migrationBuilder.DropColumn(
                name: "AssignedEquipmentId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "EquipmentCode",
                table: "WorkOrderResponses");

            migrationBuilder.DropColumn(
                name: "EquipmentId",
                table: "WorkOrderResponses");

            migrationBuilder.DropColumn(
                name: "EquipmentName",
                table: "WorkOrderResponses");

            migrationBuilder.DropColumn(
                name: "NewEquipmentCode",
                table: "WorkOrderResponseCorrections");

            migrationBuilder.DropColumn(
                name: "NewEquipmentName",
                table: "WorkOrderResponseCorrections");

            migrationBuilder.DropColumn(
                name: "PreviousEquipmentCode",
                table: "WorkOrderResponseCorrections");

            migrationBuilder.DropColumn(
                name: "PreviousEquipmentName",
                table: "WorkOrderResponseCorrections");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "EquipmentProcessSegments");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentProcessSegments_ProcessSegmentId",
                table: "EquipmentProcessSegments",
                column: "ProcessSegmentId");
        }
    }
}
