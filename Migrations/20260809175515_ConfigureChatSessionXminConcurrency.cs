using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureChatSessionXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL exposes xmin as a system column. The model maps it as
            // a concurrency token; no schema change is required or valid.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
