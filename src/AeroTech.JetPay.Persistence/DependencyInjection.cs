using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Framework.Infrastructure.HealthChecks;
using AeroTech.JetPay.Domain.IdempotencyRecordAggregate.Contracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.Contracts;
using AeroTech.JetPay.Persistence.IdempotencyRecordAggregate;
using AeroTech.JetPay.Persistence.Inbox;
using AeroTech.JetPay.Persistence.Outbox;
using AeroTech.JetPay.Persistence.PaymentIntentAggregate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.JetPay.Persistence
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("CommandDbContext")
                                   ?? configuration.GetConnectionString("JetPayDbContext");

            services.AddDbContext<JetPayDbContext>(options => options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(JetPayDbContext.MigrationsHistoryTable, JetPayDbContext.MigrationsHistorySchema)));

            services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<JetPayDbContext>());
            services.AddScoped<IPaymentIntentRepository, PaymentIntentRepository>();
            services.AddScoped<IIdempotencyRecordRepository, IdempotencyRecordRepository>();

            services.Configure<IntegrationEventOptions>(configuration.GetSection("IntegrationEvents"));
            services.AddScoped<IOutboxWriter, OutboxWriter>();
            services.AddScoped<IInboxStore, InboxStore>();

            services.AddHealthChecks().AddDbContextReadinessCheck<JetPayDbContext>("sql-server-command");

            return services;
        }
    }
}
