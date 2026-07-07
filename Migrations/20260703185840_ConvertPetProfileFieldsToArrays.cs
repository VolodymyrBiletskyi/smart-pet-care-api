using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPetProfileFieldsToArrays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Pets"
                ALTER COLUMN "ChronicConditions" TYPE text[]
                USING CASE
                    WHEN "ChronicConditions" IS NULL THEN NULL
                    WHEN btrim("ChronicConditions") = '' THEN ARRAY[]::text[]
                    ELSE ARRAY["ChronicConditions"]
                END;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Pets"
                ALTER COLUMN "BehavioralNotes" TYPE text[]
                USING CASE
                    WHEN "BehavioralNotes" IS NULL THEN NULL
                    WHEN btrim("BehavioralNotes") = '' THEN ARRAY[]::text[]
                    ELSE ARRAY["BehavioralNotes"]
                END;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Pets"
                ALTER COLUMN "Allergies" TYPE text[]
                USING CASE
                    WHEN "Allergies" IS NULL THEN NULL
                    WHEN btrim("Allergies") = '' THEN ARRAY[]::text[]
                    ELSE ARRAY["Allergies"]
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Pets"
                ALTER COLUMN "ChronicConditions" TYPE text
                USING array_to_string("ChronicConditions", E'\n');
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Pets"
                ALTER COLUMN "BehavioralNotes" TYPE text
                USING array_to_string("BehavioralNotes", E'\n');
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Pets"
                ALTER COLUMN "Allergies" TYPE text
                USING array_to_string("Allergies", E'\n');
                """);
        }
    }
}
