using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Application.PaymentMethodOptions.Services;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Services;
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

            var paymentSessions = configuration.GetSection(PaymentSessionOptions.SectionName);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                paymentSessions.Get<PaymentSessionOptions>()?.LockExpirySeconds ?? 0,
                $"{PaymentSessionOptions.SectionName}:{nameof(PaymentSessionOptions.LockExpirySeconds)}");

            services.Configure<PaymentSessionOptions>(paymentSessions);

            services.AddScoped<IIdempotencyGuard, IdempotencyGuard>();
            services.AddScoped<IPaymentSessionLock, PaymentSessionLock>();
            services.AddScoped<ITenderProviderResolver, TenderProviderResolver>();
            services.AddScoped<IPaymentMethodOptionResolver, PaymentMethodOptionResolver>();
            services.AddScoped<IPaymentSelectionPlanner, PaymentSelectionPlanner>();
            services.AddScoped<IPaymentIntentExecution, PaymentIntentExecution>();

            return services;
        }
    }
}
