using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class SwitchNutritionAnalysisToFeedingSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old shape graded a day as Grade/Score/Summary/Advice; the new
            // one grades it as Status/Target/Actual/Deviation. No column maps
            // onto another — a 0-100 Score says nothing about a calorie target —
            // so existing rows cannot be carried across and are dropped rather
            // than left as zeroed-out records the API would serve as real
            // analyses. Nothing is lost that cannot be regenerated: only the two
            // most recent analyses per pet are kept, and re-running the endpoint
            // rebuilds them.
            migrationBuilder.Sql("DELETE FROM \"NutritionAnalyses\";");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NutritionAnalyses_Score",
                table: "NutritionAnalyses");

            migrationBuilder.DropColumn(
                name: "Advice",
                table: "NutritionAnalyses");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "NutritionAnalyses");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "NutritionAnalyses");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "NutritionAnalyses");

            // Scaffolded as a rename of TotalCalories, which would have written
            // a calorie total into an enum column. They are unrelated fields.
            migrationBuilder.DropColumn(
                name: "TotalCalories",
                table: "NutritionAnalyses");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "NutritionAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualCalories",
                table: "NutritionAnalyses",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeviationPct",
                table: "NutritionAnalyses",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetCalories",
                table: "NutritionAnalyses",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NutritionAnalyses_NonNegativeCalories",
                table: "NutritionAnalyses",
                sql: "\"TargetCalories\" >= 0 AND \"ActualCalories\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Symmetrical with Up: the rows cannot be expressed in the old shape
            // either, so the table is emptied rather than back-filled.
            migrationBuilder.Sql("DELETE FROM \"NutritionAnalyses\";");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NutritionAnalyses_NonNegativeCalories",
                table: "NutritionAnalyses");

            migrationBuilder.DropColumn(
                name: "ActualCalories",
                table: "NutritionAnalyses");

            migrationBuilder.DropColumn(
                name: "DeviationPct",
                table: "NutritionAnalyses");

            migrationBuilder.DropColumn(
                name: "TargetCalories",
                table: "NutritionAnalyses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "NutritionAnalyses");

            migrationBuilder.AddColumn<int>(
                name: "TotalCalories",
                table: "NutritionAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<List<string>>(
                name: "Advice",
                table: "NutritionAnalyses",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "Grade",
                table: "NutritionAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "NutritionAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "NutritionAnalyses",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NutritionAnalyses_Score",
                table: "NutritionAnalyses",
                sql: "\"Score\" >= 0 AND \"Score\" <= 100");
        }
    }
}
