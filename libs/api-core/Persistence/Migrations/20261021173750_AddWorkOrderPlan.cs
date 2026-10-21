using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedAt",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedEndAt",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedStartAt",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedAt",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PlannedEndAt",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PlannedStartAt",
                table: "WorkOrders");
        }
    }
}
