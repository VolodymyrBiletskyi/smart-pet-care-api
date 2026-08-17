using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderCompletionRecalc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalN",
                table: "Reminders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCompletedAt",
                table: "Reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueSince",
                table: "Reminders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecalcStrategy",
                table: "Reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduleAnchorAt",
                table: "Reminders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ReminderRun",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PerformedAt",
                table: "ReminderRun",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ReminderRun",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReminderId",
                table: "HealthRecords",
                type: "uuid",
                nullable: true);

            // Existing rules have always counted from their first occurrence, so that is where
            // the anchor starts. Without this they would all anchor on 0001-01-01 and any
            // interval above 1 would compute its parity from the wrong origin.
            migrationBuilder.Sql("""
                UPDATE "Reminders" SET "ScheduleAnchorAt" = "StartAt";
                """);

            // A run and the advancing of NextTriggerAt are written in one SaveChanges, so two
            // rows for one slot should not exist. Check anyway: a bare unique-violation here
            // would be a cryptic way to fail a production migration.
            migrationBuilder.Sql("""
                DO $$
                DECLARE duplicates int;
                BEGIN
                    SELECT count(*) INTO duplicates FROM (
                        SELECT "ReminderId", "ScheduledFor"
                        FROM "ReminderRun"
                        GROUP BY "ReminderId", "ScheduledFor"
                        HAVING count(*) > 1
                    ) d;

                    IF duplicates > 0 THEN
                        RAISE EXCEPTION
                            'ReminderRun has % duplicate (ReminderId, ScheduledFor) pairs; resolve them before applying this migration.',
                            duplicates;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_PetId_OverdueSince",
                table: "Reminders",
                columns: new[] { "PetId", "OverdueSince" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reminders_IntervalN",
                table: "Reminders",
                sql: "\"IntervalN\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderRun_PerformedAt",
                table: "ReminderRun",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderRun_ReminderId_ScheduledFor",
                table: "ReminderRun",
                columns: new[] { "ReminderId", "ScheduledFor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthRecords_ReminderId",
                table: "HealthRecords",
                column: "ReminderId");

            migrationBuilder.AddForeignKey(
                name: "FK_HealthRecords_Reminders_ReminderId",
                table: "HealthRecords",
                column: "ReminderId",
                principalTable: "Reminders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HealthRecords_Reminders_ReminderId",
                table: "HealthRecords");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_PetId_OverdueSince",
                table: "Reminders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reminders_IntervalN",
                table: "Reminders");

            migrationBuilder.DropIndex(
                name: "IX_ReminderRun_PerformedAt",
                table: "ReminderRun");

            migrationBuilder.DropIndex(
                name: "IX_ReminderRun_ReminderId_ScheduledFor",
                table: "ReminderRun");

            migrationBuilder.DropIndex(
                name: "IX_HealthRecords_ReminderId",
                table: "HealthRecords");

            migrationBuilder.DropColumn(
                name: "IntervalN",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "LastCompletedAt",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "OverdueSince",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "RecalcStrategy",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "ScheduleAnchorAt",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ReminderRun");

            migrationBuilder.DropColumn(
                name: "PerformedAt",
                table: "ReminderRun");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ReminderRun");

            migrationBuilder.DropColumn(
                name: "ReminderId",
                table: "HealthRecords");
        }
    }
}
