using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderToolingAndSetupValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrderSetupValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkOrderId = table.Column<int>(type: "integer", nullable: false),
                    SetupAttributeId = table.Column<int>(type: "integer", nullable: false),
                    AttributeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AttributeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AttributeChangeMinutes = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderSetupValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderSetupValues_SetupAttributes_SetupAttributeId",
                        column: x => x.SetupAttributeId,
                        principalTable: "SetupAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSetupValues_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderTooling",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkOrderId = table.Column<int>(type: "integer", nullable: false),
                    ToolingId = table.Column<int>(type: "integer", nullable: false),
                    ToolingCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToolingName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ToolingSetupMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderTooling", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderTooling_Tooling_ToolingId",
                        column: x => x.ToolingId,
                        principalTable: "Tooling",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderTooling_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSetupValues_SetupAttributeId",
                table: "WorkOrderSetupValues",
                column: "SetupAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSetupValues_WorkOrderId_SetupAttributeId",
                table: "WorkOrderSetupValues",
                columns: new[] { "WorkOrderId", "SetupAttributeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTooling_ToolingId",
                table: "WorkOrderTooling",
                column: "ToolingId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTooling_WorkOrderId_ToolingId",
                table: "WorkOrderTooling",
                columns: new[] { "WorkOrderId", "ToolingId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderSetupValues");

            migrationBuilder.DropTable(
                name: "WorkOrderTooling");
        }
    }
}
