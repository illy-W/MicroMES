using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderPlannedSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PlannedSetupIsFull",
                table: "WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PlannedSetupMinutes",
                table: "WorkOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "PlannedSetupReasons",
                table: "WorkOrders",
                type: "text[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedSetupIsFull",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PlannedSetupMinutes",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PlannedSetupReasons",
                table: "WorkOrders");
        }
    }
}
