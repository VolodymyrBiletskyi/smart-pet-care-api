using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using smart_pet_care_api.Data;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260704113000_EnsurePetPhotoPublicId")]
    public partial class EnsurePetPhotoPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Pets'
                          AND column_name = 'PhotoPublicId'
                    ) THEN
                        ALTER TABLE "Pets" ADD "PhotoPublicId" text;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Pets'
                          AND column_name = 'PhotoPublicId'
                    ) THEN
                        ALTER TABLE "Pets" DROP COLUMN "PhotoPublicId";
                    END IF;
                END $$;
                """);
        }
    }
}
