using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<ExternalLogin> ExternalLogins { get; set; }
        public DbSet<Pet> Pets { get; set; }
        public DbSet<PetFile> PetFiles { get; set; }
        public DbSet<PetCondition> PetConditions { get; set; }
        public DbSet<PetMedication> PetMedications { get; set; }
        public DbSet<PetMedicationScheduleTime> PetMedicationScheduleTimes { get; set; }
        public DbSet<Reminder> Reminders { get; set; }
        public DbSet<ReminderRun> ReminderRuns { get; set; }
        public DbSet<PetEvent> PetEvents { get; set; }
        public DbSet<FeedingLog> FeedingLogs { get; set; }
        public DbSet<AiSession> AiSessions { get; set; }
        public DbSet<AiMessage> AiMessages { get; set; }
        public DbSet<ActivityDaily> ActivityDailies { get; set; }
        public DbSet<DeviceToken> DeviceTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // Keep the table name EF discovered via navigation property in the initial migration
            modelBuilder.Entity<ReminderRun>().ToTable("ReminderRun");
        }
    }
}