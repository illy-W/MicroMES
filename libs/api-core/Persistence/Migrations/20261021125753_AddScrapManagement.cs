using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroMES.ApiCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScrapReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderResponseCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkOrderResponseId = table.Column<int>(type: "integer", nullable: false),
                    CorrectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PreviousGoodQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PreviousScrapQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PreviousScrapDetail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NewGoodQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NewScrapQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NewScrapDetail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderResponseCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderResponseCorrections_WorkOrderResponses_WorkOrderRe~",
                        column: x => x.WorkOrderResponseId,
                        principalTable: "WorkOrderResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScrapReasonProcessSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScrapReasonId = table.Column<int>(type: "integer", nullable: false),
                    ProcessSegmentId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapReasonProcessSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrapReasonProcessSegments_ProcessSegments_ProcessSegmentId",
                        column: x => x.ProcessSegmentId,
                        principalTable: "ProcessSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScrapReasonProcessSegments_ScrapReasons_ScrapReasonId",
                        column: x => x.ScrapReasonId,
                        principalTable: "ScrapReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderResponseScraps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkOrderResponseId = table.Column<int>(type: "integer", nullable: false),
                    ScrapReasonId = table.Column<int>(type: "integer", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReasonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderResponseScraps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderResponseScraps_ScrapReasons_ScrapReasonId",
                        column: x => x.ScrapReasonId,
                        principalTable: "ScrapReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkOrderResponseScraps_WorkOrderResponses_WorkOrderRespons~",
                        column: x => x.WorkOrderResponseId,
                        principalTable: "WorkOrderResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReasonProcessSegments_ProcessSegmentId",
                table: "ScrapReasonProcessSegments",
                column: "ProcessSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReasonProcessSegments_ScrapReasonId_ProcessSegmentId",
                table: "ScrapReasonProcessSegments",
                columns: new[] { "ScrapReasonId", "ProcessSegmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScrapReasons_Code",
                table: "ScrapReasons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderResponseCorrections_WorkOrderResponseId",
                table: "WorkOrderResponseCorrections",
                column: "WorkOrderResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderResponseScraps_ScrapReasonId",
                table: "WorkOrderResponseScraps",
                column: "ScrapReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderResponseScraps_WorkOrderResponseId",
                table: "WorkOrderResponseScraps",
                column: "WorkOrderResponseId");

            // Existing sessions with scrap keep one "no reason" row, so reports can group every scrapped piece.
            migrationBuilder.Sql(
                "INSERT INTO \"WorkOrderResponseScraps\" (\"WorkOrderResponseId\", \"Quantity\") " +
                "SELECT \"Id\", \"ScrapQuantity\" FROM \"WorkOrderResponses\" WHERE \"ScrapQuantity\" > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrapReasonProcessSegments");

            migrationBuilder.DropTable(
                name: "WorkOrderResponseCorrections");

            migrationBuilder.DropTable(
                name: "WorkOrderResponseScraps");

            migrationBuilder.DropTable(
                name: "ScrapReasons");
        }
    }
}
