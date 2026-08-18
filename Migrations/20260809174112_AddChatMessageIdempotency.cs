using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassifierResponseJson",
                table: "ChatMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientMessageId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId_ClientMessageId",
                table: "ChatMessages",
                columns: new[] { "SessionId", "ClientMessageId" },
                unique: true,
                filter: "\"ClientMessageId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SessionId_ClientMessageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ClassifierResponseJson",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "ChatMessages");
        }
    }
}
