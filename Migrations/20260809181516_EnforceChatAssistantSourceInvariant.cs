using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceChatAssistantSourceInvariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatMessages_SourceMessageId_AssistantOnly",
                table: "ChatMessages",
                sql: "\"Role\" = 1 OR \"SourceMessageId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatMessages_SourceMessageId_AssistantOnly",
                table: "ChatMessages");
        }
    }
}
