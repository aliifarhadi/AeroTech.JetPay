using AeroTech.JetPay.ReferenceData.Configuration;
using AeroTech.JetPay.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AeroTech.JetPay.ReferenceData
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddReferenceData(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ReferenceDataOptions>(configuration.GetSection(ReferenceDataOptions.SectionName));

            var connectionString = configuration.GetConnectionString("QueryDbContext")
                                   ?? configuration.GetConnectionString("CommandDbContext")
                                   ?? configuration.GetConnectionString("JetPayDbContext");

            services.AddDbContext<ReferenceDbContext>(options => options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(ReferenceDbContext.MigrationsHistoryTable, ReferenceDbContext.MigrationsHistorySchema)));

            services.TryAddSingleton(TimeProvider.System);

            return services;
        }
    }
}
