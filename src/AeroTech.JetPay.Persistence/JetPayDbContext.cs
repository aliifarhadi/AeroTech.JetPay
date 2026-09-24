using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Framework.Infrastructure.Persistence;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate;
using AeroTech.JetPay.Domain.PaymentIntentAggregate;
using AeroTech.JetPay.Persistence.Inbox;
using AeroTech.JetPay.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.JetPay.Persistence
{
    public sealed class JetPayDbContext : CommandDbContext, IUnitOfWork
    {
        public const string MigrationsHistorySchema = "dbo";
        public const string MigrationsHistoryTable = "__CommandsMigrationHistory";

        public JetPayDbContext(
            DbContextOptions<JetPayDbContext> options,
            IIdentityService identityService,
            IClock clock,
            IDomainEventDispatcher domainEventDispatcher)
            : base(options, identityService, clock, domainEventDispatcher)
        {
        }

        public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();

        public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("Payment");
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(JetPayDbContext).Assembly);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Four decimals keep three-decimal currencies exact; amounts are never rounded by the store.
            configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
            configurationBuilder.Properties<string>().HaveMaxLength(256);
        }
    }
}
