using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NutritionGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyCalorieTarget = table.Column<int>(type: "integer", nullable: true),
                    DailyPortionTarget = table.Column<decimal>(type: "numeric", nullable: true),
                    PortionUnit = table.Column<int>(type: "integer", nullable: true),
                    MealsPerDay = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionGoals", x => x.Id);
                    table.CheckConstraint("CK_NutritionGoals_NonNegative", "(\"DailyCalorieTarget\" IS NULL OR \"DailyCalorieTarget\" >= 0) AND (\"DailyPortionTarget\" IS NULL OR \"DailyPortionTarget\" >= 0) AND (\"MealsPerDay\" IS NULL OR \"MealsPerDay\" >= 0)");
                    table.ForeignKey(
                        name: "FK_NutritionGoals_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NutritionGoals_PetId",
                table: "NutritionGoals",
                column: "PetId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NutritionGoals");
        }
    }
}
