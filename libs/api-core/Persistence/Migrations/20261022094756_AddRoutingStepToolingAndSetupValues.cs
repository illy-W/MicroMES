using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutingStepToolingAndSetupValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoutingStepSetupValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoutingStepId = table.Column<int>(type: "integer", nullable: false),
                    SetupAttributeId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingStepSetupValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutingStepSetupValues_RoutingSteps_RoutingStepId",
                        column: x => x.RoutingStepId,
                        principalTable: "RoutingSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoutingStepSetupValues_SetupAttributes_SetupAttributeId",
                        column: x => x.SetupAttributeId,
                        principalTable: "SetupAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoutingStepTooling",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoutingStepId = table.Column<int>(type: "integer", nullable: false),
                    ToolingId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingStepTooling", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutingStepTooling_RoutingSteps_RoutingStepId",
                        column: x => x.RoutingStepId,
                        principalTable: "RoutingSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoutingStepTooling_Tooling_ToolingId",
                        column: x => x.ToolingId,
                        principalTable: "Tooling",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingStepSetupValues_RoutingStepId_SetupAttributeId",
                table: "RoutingStepSetupValues",
                columns: new[] { "RoutingStepId", "SetupAttributeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingStepSetupValues_SetupAttributeId",
                table: "RoutingStepSetupValues",
                column: "SetupAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingStepTooling_RoutingStepId_ToolingId",
                table: "RoutingStepTooling",
                columns: new[] { "RoutingStepId", "ToolingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingStepTooling_ToolingId",
                table: "RoutingStepTooling",
                column: "ToolingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutingStepSetupValues");

            migrationBuilder.DropTable(
                name: "RoutingStepTooling");
        }
    }
}
