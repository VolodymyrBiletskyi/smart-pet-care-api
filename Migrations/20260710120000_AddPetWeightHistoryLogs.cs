using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using smart_pet_care_api.Data;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260710120000_AddPetWeightHistoryLogs")]
    public partial class AddPetWeightHistoryLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PetWeightLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric", nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetWeightLogs", x => x.Id);
                    table.CheckConstraint("CK_PetWeightLogs_WeightKg_Positive", "\"WeightKg\" > 0 AND \"WeightKg\" <= 230");
                    table.ForeignKey(
                        name: "FK_PetWeightLogs_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "PetWeightLogs" ("Id", "PetId", "WeightKg", "MeasuredAt", "Notes", "CreatedAt")
                SELECT
                    "Id",
                    "Id",
                    "WeightKg",
                    COALESCE("UpdatedAt", "CreatedAt", now()),
                    'Initial weight backfilled from existing pet record',
                    now()
                FROM "Pets"
                WHERE "WeightKg" IS NOT NULL
                    AND "WeightKg" > 0
                    AND "WeightKg" <= 230;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PetWeightLogs_PetId_MeasuredAt",
                table: "PetWeightLogs",
                columns: new[] { "PetId", "MeasuredAt" },
                unique: true,
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PetWeightLogs");
        }
    }
}
