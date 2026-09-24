using AeroTech.Framework.Infrastructure.HealthChecks;
using AeroTech.JetPay.Query._Shared.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.JetPay.Query
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddQuery(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("QueryDbContext")
                                   ?? configuration.GetConnectionString("CommandDbContext")
                                   ?? configuration.GetConnectionString("JetPayDbContext");

            services.AddDbContext<QueryDbContext>(options => options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(QueryDbContext.MigrationsHistoryTable, QueryDbContext.MigrationsHistorySchema)));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

            services.AddHealthChecks().AddDbContextReadinessCheck<QueryDbContext>("sql-server-query");

            return services;
        }
    }
}
