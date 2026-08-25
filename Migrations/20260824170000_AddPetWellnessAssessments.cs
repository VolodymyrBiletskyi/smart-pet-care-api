using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using smart_pet_care_api.Data;

#nullable disable

namespace smart_pet_care_api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260824170000_AddPetWellnessAssessments")]
public partial class AddPetWellnessAssessments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PetWellnessAssessments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PetId = table.Column<Guid>(type: "uuid", nullable: false),
                WellnessScore = table.Column<int>(type: "integer", nullable: true),
                Band = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                ScoreStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DataCoverage = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                CalculationVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                WindowStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                WindowEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                Trend = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PetWellnessAssessments", x => x.Id);
                table.CheckConstraint("CK_PetWellnessAssessments_Coverage", "\"DataCoverage\" >= 0 AND \"DataCoverage\" <= 1");
                table.CheckConstraint("CK_PetWellnessAssessments_Score", "\"WellnessScore\" IS NULL OR (\"WellnessScore\" >= 0 AND \"WellnessScore\" <= 100)");
                table.ForeignKey(
                    name: "FK_PetWellnessAssessments_Pets_PetId",
                    column: x => x.PetId,
                    principalTable: "Pets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PetWellnessAssessments_PetId_EvaluatedAt",
            table: "PetWellnessAssessments",
            columns: new[] { "PetId", "EvaluatedAt" },
            descending: new[] { false, true });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "PetWellnessAssessments");
}
