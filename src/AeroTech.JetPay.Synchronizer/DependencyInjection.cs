using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.JetPay.Synchronizer
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSynchronizer(this IServiceCollection services)
        {
            return services;
        }
    }
}
