using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class RefactorReminderSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "Interval",
                table: "Reminders");

            migrationBuilder.AddColumn<int[]>(
                name: "Days",
                table: "Reminders",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepeatable",
                table: "Reminders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "TimeOfDay",
                table: "Reminders",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Days",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "IsRepeatable",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "TimeOfDay",
                table: "Reminders");

            migrationBuilder.AddColumn<int>(
                name: "Frequency",
                table: "Reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Interval",
                table: "Reminders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
