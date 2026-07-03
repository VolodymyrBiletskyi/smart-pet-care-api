using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderRepeatModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "Reminders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatType",
                table: "Reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UtcOffsetMinutes",
                table: "Reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill from the old IsRepeatable flag: repeatable -> Weekly (0), one-off -> Once (2)
            // with the date taken from the existing StartAt.
            migrationBuilder.Sql(
                "UPDATE \"Reminders\" SET \"RepeatType\" = CASE WHEN \"IsRepeatable\" THEN 0 ELSE 2 END;");
            migrationBuilder.Sql(
                "UPDATE \"Reminders\" SET \"Date\" = (\"StartAt\")::date WHERE \"IsRepeatable\" = false;");

            migrationBuilder.DropColumn(
                name: "IsRepeatable",
                table: "Reminders");

            // The Interval column was dropped in RefactorReminderSchedule, which cascaded away this
            // constraint in the DB even though it lingered in the EF model. Drop it idempotently.
            migrationBuilder.Sql(
                "ALTER TABLE \"Reminders\" DROP CONSTRAINT IF EXISTS \"CK_Reminders_Interval_Positive\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRepeatable",
                table: "Reminders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Reverse the backfill: Weekly (0) -> repeatable, everything else -> one-off.
            migrationBuilder.Sql(
                "UPDATE \"Reminders\" SET \"IsRepeatable\" = (\"RepeatType\" = 0);");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "RepeatType",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "UtcOffsetMinutes",
                table: "Reminders");

            // Not restoring CK_Reminders_Interval_Positive: the Interval column it referenced no
            // longer exists, so re-adding it would fail.
        }
    }
}
