using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAssistantSourceMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceMessageId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SourceMessageId",
                table: "ChatMessages",
                column: "SourceMessageId",
                unique: true,
                filter: "\"SourceMessageId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SourceMessageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SourceMessageId",
                table: "ChatMessages");
        }
    }
}
