using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityTypeIntensityDurationAndSleepLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "ActivityLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Intensity",
                table: "ActivityLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ActivityLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SleepLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SleepDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SleepLogs", x => x.Id);
                    table.CheckConstraint("CK_SleepLogs_HoursInRange", "\"Hours\" > 0 AND \"Hours\" <= 24");
                    table.ForeignKey(
                        name: "FK_SleepLogs_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ActivityLogs_DurationMinutesInRange",
                table: "ActivityLogs",
                sql: "\"DurationMinutes\" IS NULL OR (\"DurationMinutes\" > 0 AND \"DurationMinutes\" <= 1440)");

            migrationBuilder.CreateIndex(
                name: "IX_SleepLogs_PetId_SleepDate",
                table: "SleepLogs",
                columns: new[] { "PetId", "SleepDate" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SleepLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ActivityLogs_DurationMinutesInRange",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Intensity",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ActivityLogs");
        }
    }
}
