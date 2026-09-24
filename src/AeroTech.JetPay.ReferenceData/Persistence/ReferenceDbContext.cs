using AeroTech.JetPay.ReferenceData.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.JetPay.ReferenceData.Persistence
{
    public sealed class ReferenceDbContext : DbContext
    {
        public const string Schema = "ReferenceData";
        public const string MigrationsHistorySchema = "dbo";
        public const string MigrationsHistoryTable = "__ReferenceDataMigrationHistory";

        public ReferenceDbContext(DbContextOptions<ReferenceDbContext> options) : base(options)
        {
        }

        public DbSet<ReferenceSyncState> SyncStates => Set<ReferenceSyncState>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.Entity<ReferenceSyncState>(entity =>
            {
                entity.ToTable("ReferenceDataSyncStates");
                entity.HasKey(state => state.Id);
                entity.Property(state => state.Id).HasMaxLength(64).ValueGeneratedNever();
            });

            base.OnModelCreating(modelBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<string>().HaveMaxLength(256);
        }
    }
}
