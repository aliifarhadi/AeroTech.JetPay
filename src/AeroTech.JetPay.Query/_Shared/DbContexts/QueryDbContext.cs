using AeroTech.JetPay.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.JetPay.Query._Shared.DbContexts
{
    public sealed class QueryDbContext : DbContext
    {
        public const string ReadModelSchema = "ReadModel";
        public const string WriteModelSchema = "Payment";
        public const string MigrationsHistorySchema = "dbo";
        public const string MigrationsHistoryTable = "__QueriesMigrationHistory";

        public QueryDbContext(DbContextOptions<QueryDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(ReadModelSchema);
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(QueryDbContext).Assembly);
        }

        private static void MapWriteModel<TEntity>(ModelBuilder modelBuilder, string table)
            where TEntity : class
            => modelBuilder.Entity<TEntity>(entity =>
            {
                entity.ToTable(table, WriteModelSchema, builder => builder.ExcludeFromMigrations());
                entity.HasKey("Id");
            });

        private static void MapReferenceReadModel<TEntity>(ModelBuilder modelBuilder, string table)
            where TEntity : class
            => modelBuilder.Entity<TEntity>(entity =>
            {
                entity.ToTable(table, ReferenceDbContext.Schema, builder => builder.ExcludeFromMigrations());
                entity.HasKey("Id");
            });

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
            configurationBuilder.Properties<string>().HaveMaxLength(256);
        }
    }
}
