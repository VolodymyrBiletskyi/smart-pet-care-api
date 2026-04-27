using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smart_pet_care_api.Migrations
{
    /// <inheritdoc />
    public partial class initialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalLogins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ProviderUserId = table.Column<string>(type: "text", nullable: false),
                    ProviderEmail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Species = table.Column<string>(type: "text", nullable: false),
                    Breed = table.Column<string>(type: "text", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "date", nullable: true),
                    Sex = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric", nullable: true),
                    BehavioralNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pets", x => x.Id);
                    table.CheckConstraint("CK_Pets_WeightKg_Positive", "\"WeightKg\" IS NULL OR \"WeightKg\" > 0");
                    table.ForeignKey(
                        name: "FK_Pets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityDailies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Steps = table.Column<int>(type: "integer", nullable: true),
                    ActiveMinutes = table.Column<decimal>(type: "numeric", nullable: true),
                    SleepHours = table.Column<decimal>(type: "numeric", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    RawPayload = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityDailies", x => x.Id);
                    table.CheckConstraint("CK_ActivityDailies_NonNegative", "(\"Steps\" IS NULL OR \"Steps\" >= 0) AND (\"ActiveMinutes\" IS NULL OR \"ActiveMinutes\" >= 0) AND (\"SleepHours\" IS NULL OR \"SleepHours\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ActivityDailies_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PetId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiSessions_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FoodType = table.Column<int>(type: "integer", nullable: false),
                    FoodName = table.Column<string>(type: "text", nullable: true),
                    PortionAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    PortionUnit = table.Column<int>(type: "integer", nullable: true),
                    ApproxCalories = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedingLogs", x => x.Id);
                    table.CheckConstraint("CK_FeedingLogs_NonNegative", "(\"PortionAmount\" IS NULL OR \"PortionAmount\" >= 0) AND (\"ApproxCalories\" IS NULL OR \"ApproxCalories\" >= 0)");
                    table.ForeignKey(
                        name: "FK_FeedingLogs_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PetConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Allergen = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PetConditions_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PetEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsSystemGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetEvents", x => x.Id);
                    table.CheckConstraint("CK_PetEvents_DateRange", "\"EndAt\" IS NULL OR \"EndAt\" >= \"ScheduledAt\"");
                    table.ForeignKey(
                        name: "FK_PetEvents_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PetFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetFiles", x => x.Id);
                    table.CheckConstraint("CK_PetFiles_Size_NonNegative", "\"Size\" >= 0");
                    table.ForeignKey(
                        name: "FK_PetFiles_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PetFiles_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PetMedications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Dosage = table.Column<string>(type: "text", nullable: true),
                    Instructions = table.Column<string>(type: "text", nullable: true),
                    MedicationFrequency = table.Column<int>(type: "integer", nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetMedications", x => x.Id);
                    table.CheckConstraint("CK_PetMedications_DateRange", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("CK_PetMedications_Interval_Positive", "\"Interval\" > 0");
                    table.ForeignKey(
                        name: "FK_PetMedications_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextTriggerAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    IsSystemGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                    table.CheckConstraint("CK_Reminders_DateRange", "\"EndAt\" IS NULL OR \"EndAt\" >= \"StartAt\"");
                    table.CheckConstraint("CK_Reminders_Interval_Positive", "\"Interval\" > 0");
                    table.ForeignKey(
                        name: "FK_Reminders_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiMessages_AiSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PetMedicationScheduleTimes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetMedicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeOfDay = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetMedicationScheduleTimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PetMedicationScheduleTimes_PetMedications_PetMedicationId",
                        column: x => x.PetMedicationId,
                        principalTable: "PetMedications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReminderRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReminderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: true),
                    DeliveryMeta = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderRun", x => x.Id);
                    table.CheckConstraint("CK_ReminderRun_TimingOrder", "(\"SentAt\" IS NULL OR \"SentAt\" >= \"ScheduledFor\") AND (\"CompletedAt\" IS NULL OR \"SentAt\" IS NULL OR \"CompletedAt\" >= \"SentAt\")");
                    table.ForeignKey(
                        name: "FK_ReminderRun_Reminders_ReminderId",
                        column: x => x.ReminderId,
                        principalTable: "Reminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityDailies_PetId_ActivityDate",
                table: "ActivityDailies",
                columns: new[] { "PetId", "ActivityDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityDailies_PetId_ActivityDate_Source",
                table: "ActivityDailies",
                columns: new[] { "PetId", "ActivityDate", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_SessionId_CreatedAt",
                table: "AiMessages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiSessions_PetId",
                table: "AiSessions",
                column: "PetId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSessions_UserId",
                table: "AiSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSessions_UserId_CreatedAt",
                table: "AiSessions",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_Provider_ProviderUserId",
                table: "ExternalLogins",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_UserId",
                table: "ExternalLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedingLogs_PetId_FedAt",
                table: "FeedingLogs",
                columns: new[] { "PetId", "FedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PetConditions_PetId",
                table: "PetConditions",
                column: "PetId");

            migrationBuilder.CreateIndex(
                name: "IX_PetConditions_PetId_IsActive",
                table: "PetConditions",
                columns: new[] { "PetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PetEvents_PetId_ScheduledAt",
                table: "PetEvents",
                columns: new[] { "PetId", "ScheduledAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PetEvents_PetId_Status",
                table: "PetEvents",
                columns: new[] { "PetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PetEvents_SourceType_SourceId",
                table: "PetEvents",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PetFiles_PetId",
                table: "PetFiles",
                column: "PetId");

            migrationBuilder.CreateIndex(
                name: "IX_PetFiles_StorageKey",
                table: "PetFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PetFiles_UploadedByUserId",
                table: "PetFiles",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PetMedications_PetId",
                table: "PetMedications",
                column: "PetId");

            migrationBuilder.CreateIndex(
                name: "IX_PetMedications_PetId_StartDate",
                table: "PetMedications",
                columns: new[] { "PetId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PetMedicationScheduleTimes_PetMedicationId",
                table: "PetMedicationScheduleTimes",
                column: "PetMedicationId");

            migrationBuilder.CreateIndex(
                name: "IX_PetMedicationScheduleTimes_PetMedicationId_TimeOfDay",
                table: "PetMedicationScheduleTimes",
                columns: new[] { "PetMedicationId", "TimeOfDay" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pets_UserId",
                table: "Pets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderRun_ReminderId",
                table: "ReminderRun",
                column: "ReminderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderRun_Status_ScheduledFor",
                table: "ReminderRun",
                columns: new[] { "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_PetId_NextTriggerAt",
                table: "Reminders",
                columns: new[] { "PetId", "NextTriggerAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_SourceType_SourceId",
                table: "Reminders",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_Status_NextTriggerAt",
                table: "Reminders",
                columns: new[] { "Status", "NextTriggerAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityDailies");

            migrationBuilder.DropTable(
                name: "AiMessages");

            migrationBuilder.DropTable(
                name: "ExternalLogins");

            migrationBuilder.DropTable(
                name: "FeedingLogs");

            migrationBuilder.DropTable(
                name: "PetConditions");

            migrationBuilder.DropTable(
                name: "PetEvents");

            migrationBuilder.DropTable(
                name: "PetFiles");

            migrationBuilder.DropTable(
                name: "PetMedicationScheduleTimes");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ReminderRun");

            migrationBuilder.DropTable(
                name: "AiSessions");

            migrationBuilder.DropTable(
                name: "PetMedications");

            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "Pets");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
