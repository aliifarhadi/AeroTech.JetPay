using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Clock;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Faults;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.JetPay.Mock.Tenders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AeroTech.JetPay.Mock
{
    public static class DependencyInjection
    {
        public static bool IsMockJetPayEnabled(this IConfiguration configuration)
            => configuration.GetSection(MockJetPayOptions.SectionName).GetValue<bool>(nameof(MockJetPayOptions.Enabled));

        /// <summary>
        /// Registers the mock tender adapters and test controls when MockJetPay:Enabled is true; otherwise removes the
        /// mock controllers so none of the mock surface is reachable. Call after AddFrameworkInfrastructure so the mock
        /// clock replaces the real one.
        /// </summary>
        public static IServiceCollection AddMockJetPay(this IServiceCollection services, IConfiguration configuration)
        {
            var mockAssembly = typeof(MockAssembly).Assembly;

            if (!configuration.IsMockJetPayEnabled())
            {
                services.AddControllers().ConfigureApplicationPartManager(manager =>
                {
                    foreach (var part in manager.ApplicationParts.OfType<AssemblyPart>().Where(part => part.Assembly == mockAssembly).ToList())
                        manager.ApplicationParts.Remove(part);
                });

                return services;
            }

            services.AddControllers().AddApplicationPart(mockAssembly);
            services.Configure<MockJetPayOptions>(configuration.GetSection(MockJetPayOptions.SectionName));

            services.AddSingleton<MockClock>();
            services.Replace(ServiceDescriptor.Singleton<IClock>(provider => provider.GetRequiredService<MockClock>()));

            services.AddSingleton<MockScenarioRegistry>();
            services.AddSingleton<TransportFaultInjector>();

            services.AddScoped<ITenderProvider, MockIranianPgwProvider>();
            services.AddScoped<ITenderProvider, MockStoredValueProvider>();
            services.AddScoped<ITenderProvider, MockBnplProvider>();
            services.AddScoped<ITenderProvider, MockAgencyCreditProvider>();
            services.AddScoped<IPaymentMethodOptionCatalog, MockPaymentMethodOptionCatalog>();

            return services;
        }

        public static WebApplication UseMockJetPay(this WebApplication app)
        {
            if (app.Configuration.IsMockJetPayEnabled())
                app.UseMiddleware<SimulatedTransportTimeoutMiddleware>();

            return app;
        }
    }
}
