using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class MakeDeviceTokenUniquePerDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceTokens_UserId_Token",
                table: "DeviceTokens");

            // The old index allowed one token under several accounts, so the unique one
            // cannot be created until any such rows are collapsed. The survivor is the
            // most recently seen registration -- the account currently signed in on that
            // device -- with CreatedAt and Id breaking ties so the result does not depend
            // on scan order. Migrations run at startup, and a failed one means the app
            // never listens, so this cannot be left to chance even though production has
            // no duplicates today.
            migrationBuilder.Sql("""
                DELETE FROM "DeviceTokens"
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM (
                        SELECT "Id", row_number() OVER (
                            PARTITION BY "Token"
                            ORDER BY "LastSeenAt" DESC, "CreatedAt" DESC, "Id" DESC
                        ) AS rn
                        FROM "DeviceTokens"
                    ) ranked
                    WHERE rn > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_Token",
                table: "DeviceTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_UserId",
                table: "DeviceTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceTokens_Token",
                table: "DeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_DeviceTokens_UserId",
                table: "DeviceTokens");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_UserId_Token",
                table: "DeviceTokens",
                columns: new[] { "UserId", "Token" },
                unique: true);
        }
    }
}
