using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoutingSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    ProcessSegmentId = table.Column<int>(type: "integer", nullable: false),
                    OperationNumber = table.Column<int>(type: "integer", nullable: false),
                    SetupMinutes = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    RunMinutesPerUnit = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PositionX = table.Column<double>(type: "double precision", nullable: false),
                    PositionY = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutingSteps_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoutingSteps_ProcessSegments_ProcessSegmentId",
                        column: x => x.ProcessSegmentId,
                        principalTable: "ProcessSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoutingStepDependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PredecessorStepId = table.Column<int>(type: "integer", nullable: false),
                    SuccessorStepId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingStepDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutingStepDependencies_RoutingSteps_PredecessorStepId",
                        column: x => x.PredecessorStepId,
                        principalTable: "RoutingSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoutingStepDependencies_RoutingSteps_SuccessorStepId",
                        column: x => x.SuccessorStepId,
                        principalTable: "RoutingSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingStepDependencies_PredecessorStepId_SuccessorStepId",
                table: "RoutingStepDependencies",
                columns: new[] { "PredecessorStepId", "SuccessorStepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingStepDependencies_SuccessorStepId",
                table: "RoutingStepDependencies",
                column: "SuccessorStepId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingSteps_MaterialId_OperationNumber",
                table: "RoutingSteps",
                columns: new[] { "MaterialId", "OperationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingSteps_ProcessSegmentId",
                table: "RoutingSteps",
                column: "ProcessSegmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutingStepDependencies");

            migrationBuilder.DropTable(
                name: "RoutingSteps");
        }
    }
}
