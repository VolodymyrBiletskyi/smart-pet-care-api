using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionRevocationAndCodeBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfirmationAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationAttemptsStartedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokensValidFrom",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            // Added nullable and backfilled per row rather than taking EF's single
            // default: one shared family id across every token that predates this
            // migration would mean one replayed token revoking every live session on the
            // host. gen_random_uuid() is volatile, so each existing row gets its own
            // family — which is what it is, a session whose rotation history is unknown.
            migrationBuilder.AddColumn<Guid>(
                name: "TokenFamilyId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """UPDATE "RefreshTokens" SET "TokenFamilyId" = gen_random_uuid();""");

            // Raw SQL rather than AlterColumn: the latter compares the operation against
            // an old column it assumes was already NOT NULL, so it emitted no constraint
            // at all and left the column nullable -- which a reader cannot tell from a
            // column that is meant to be. A NULL family would also be unrevokable, since
            // nothing matches it.
            migrationBuilder.Sql(
                """ALTER TABLE "RefreshTokens" ALTER COLUMN "TokenFamilyId" SET NOT NULL;""");

            migrationBuilder.AddColumn<string>(
                name: "PendingPasswordHash",
                table: "EmailConfirmationCodes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenFamilyId",
                table: "RefreshTokens",
                column: "TokenFamilyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenFamilyId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ConfirmationAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ConfirmationAttemptsStartedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokensValidFrom",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenFamilyId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "PendingPasswordHash",
                table: "EmailConfirmationCodes");
        }
    }
}
