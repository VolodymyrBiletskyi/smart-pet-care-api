using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class LinkWeightAndFeedingLogsToReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReminderId",
                table: "PetWeightLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReminderId",
                table: "FeedingLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PetWeightLogs_ReminderId",
                table: "PetWeightLogs",
                column: "ReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedingLogs_ReminderId",
                table: "FeedingLogs",
                column: "ReminderId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeedingLogs_Reminders_ReminderId",
                table: "FeedingLogs",
                column: "ReminderId",
                principalTable: "Reminders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PetWeightLogs_Reminders_ReminderId",
                table: "PetWeightLogs",
                column: "ReminderId",
                principalTable: "Reminders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedingLogs_Reminders_ReminderId",
                table: "FeedingLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_PetWeightLogs_Reminders_ReminderId",
                table: "PetWeightLogs");

            migrationBuilder.DropIndex(
                name: "IX_PetWeightLogs_ReminderId",
                table: "PetWeightLogs");

            migrationBuilder.DropIndex(
                name: "IX_FeedingLogs_ReminderId",
                table: "FeedingLogs");

            migrationBuilder.DropColumn(
                name: "ReminderId",
                table: "PetWeightLogs");

            migrationBuilder.DropColumn(
                name: "ReminderId",
                table: "FeedingLogs");
        }
    }
}
