using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Services;
using AeroTech.JetPay.Application._Shared.Behaviors;
using AeroTech.JetPay.Application._Shared.Events;
using AeroTech.JetPay.Application._Shared.Idempotency;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.JetPay.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
        {
            var assembly = typeof(DependencyInjection).Assembly;

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
            services.AddValidatorsFromAssembly(assembly);

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

            var paymentIntents = configuration.GetSection(PaymentIntentOptions.SectionName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                paymentIntents.Get<PaymentIntentOptions>()?.LockExpirySeconds ?? 0,
                $"{PaymentIntentOptions.SectionName}:{nameof(PaymentIntentOptions.LockExpirySeconds)}");

            services.Configure<PaymentIntentOptions>(paymentIntents);
            services.AddScoped<IIdempotencyGuard, IdempotencyGuard>();
            services.AddScoped<IPaymentIntentLock, PaymentIntentLock>();
            services.AddScoped<ITenderProviderResolver, TenderProviderResolver>();

            return services;
        }
    }
}
