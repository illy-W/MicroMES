using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Equipment",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "EfficiencyPercent",
                table: "Equipment",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.AddColumn<int>(
                name: "WorkCalendarId",
                table: "Equipment",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsRecurringYearly = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentExceptions_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentExceptionSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentExceptionId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentExceptionSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentExceptionSlots_EquipmentExceptions_EquipmentExcept~",
                        column: x => x.EquipmentExceptionId,
                        principalTable: "EquipmentExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkCalendarId = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsRecurringYearly = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarExceptions_WorkCalendars_WorkCalendarId",
                        column: x => x.WorkCalendarId,
                        principalTable: "WorkCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendarSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkCalendarId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendarSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkCalendarSlots_WorkCalendars_WorkCalendarId",
                        column: x => x.WorkCalendarId,
                        principalTable: "WorkCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarExceptionSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalendarExceptionId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarExceptionSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarExceptionSlots_CalendarExceptions_CalendarException~",
                        column: x => x.CalendarExceptionId,
                        principalTable: "CalendarExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_WorkCalendarId",
                table: "Equipment",
                column: "WorkCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarExceptions_WorkCalendarId",
                table: "CalendarExceptions",
                column: "WorkCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarExceptionSlots_CalendarExceptionId",
                table: "CalendarExceptionSlots",
                column: "CalendarExceptionId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentExceptions_EquipmentId",
                table: "EquipmentExceptions",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentExceptionSlots_EquipmentExceptionId",
                table: "EquipmentExceptionSlots",
                column: "EquipmentExceptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendars_Code",
                table: "WorkCalendars",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendars_IsDefault",
                table: "WorkCalendars",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendarSlots_WorkCalendarId",
                table: "WorkCalendarSlots",
                column: "WorkCalendarId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_WorkCalendars_WorkCalendarId",
                table: "Equipment",
                column: "WorkCalendarId",
                principalTable: "WorkCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The system never stays without a default calendar: STANDARD 08-12 / 13-17, Monday to Friday
            // (also on existing databases that were never seeded). Guarded, so re-running it changes nothing.
            migrationBuilder.Sql("""
                INSERT INTO "WorkCalendars" ("Code", "Name", "Description", "IsActive", "IsDefault", "CreatedAt", "UpdatedAt")
                SELECT 'STANDARD', 'Standard 8-12 / 13-17, lun-ven', 'Calendario predefinito dell''azienda', TRUE, TRUE, now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM "WorkCalendars" WHERE "IsDefault" = TRUE)
                  AND NOT EXISTS (SELECT 1 FROM "WorkCalendars" WHERE "Code" = 'STANDARD');

                INSERT INTO "WorkCalendarSlots" ("WorkCalendarId", "DayOfWeek", "StartTime", "EndTime")
                SELECT c."Id", d."Day", t."S"::time, t."E"::time
                FROM "WorkCalendars" c
                CROSS JOIN (VALUES ('Monday'), ('Tuesday'), ('Wednesday'), ('Thursday'), ('Friday')) AS d("Day")
                CROSS JOIN (VALUES ('08:00', '12:00'), ('13:00', '17:00')) AS t("S", "E")
                WHERE c."Code" = 'STANDARD' AND c."IsDefault" = TRUE
                  AND NOT EXISTS (SELECT 1 FROM "WorkCalendarSlots" s WHERE s."WorkCalendarId" = c."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_WorkCalendars_WorkCalendarId",
                table: "Equipment");

            migrationBuilder.DropTable(
                name: "CalendarExceptionSlots");

            migrationBuilder.DropTable(
                name: "EquipmentExceptionSlots");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "WorkCalendarSlots");

            migrationBuilder.DropTable(
                name: "CalendarExceptions");

            migrationBuilder.DropTable(
                name: "EquipmentExceptions");

            migrationBuilder.DropTable(
                name: "WorkCalendars");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_WorkCalendarId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "EfficiencyPercent",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "WorkCalendarId",
                table: "Equipment");
        }
    }
}
