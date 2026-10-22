using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddToolingAndSetupAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SetupAttributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangeMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupAttributes", x => x.Id);
                    table.CheckConstraint("CK_SetupAttributes_ChangeMinutes", "\"ChangeMinutes\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Tooling",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    SetupMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tooling", x => x.Id);
                    table.CheckConstraint("CK_Tooling_Quantity", "\"Quantity\" >= 1");
                    table.CheckConstraint("CK_Tooling_SetupMinutes", "\"SetupMinutes\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ToolingEquipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ToolingId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolingEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolingEquipment_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolingEquipment_Tooling_ToolingId",
                        column: x => x.ToolingId,
                        principalTable: "Tooling",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetupAttributes_Code",
                table: "SetupAttributes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tooling_Code",
                table: "Tooling",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolingEquipment_EquipmentId",
                table: "ToolingEquipment",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolingEquipment_ToolingId_EquipmentId",
                table: "ToolingEquipment",
                columns: new[] { "ToolingId", "EquipmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetupAttributes");

            migrationBuilder.DropTable(
                name: "ToolingEquipment");

            migrationBuilder.DropTable(
                name: "Tooling");
        }
    }
}
