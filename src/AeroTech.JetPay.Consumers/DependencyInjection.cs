using AeroTech.JetPay.Consumers.Jobs;
using AeroTech.JetPay.Consumers.Inbox;
using AeroTech.JetPay.Consumers.Outbox;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.JetPay.Consumers
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddConsumers(this IServiceCollection services, IConfiguration configuration)
        {
            var broker = configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();

            ArgumentException.ThrowIfNullOrWhiteSpace(broker.Server, "RabbitMq:Server");
            ArgumentException.ThrowIfNullOrWhiteSpace(broker.UserName, "RabbitMq:UserName");
            ArgumentException.ThrowIfNullOrWhiteSpace(broker.Password, "RabbitMq:Password");

            services.AddScoped(typeof(InboxConsumeFilter<>));

            services.AddMassTransit(bus =>
            {
                bus.SetKebabCaseEndpointNameFormatter();

                bus.UsingRabbitMq((context, rabbit) =>
                {
                    rabbit.Host(broker.Server, broker.Port, broker.VirtualHost, configurator =>
                    {
                        configurator.Username(broker.UserName);
                        configurator.Password(broker.Password);
                    });

                    rabbit.UseConsumeFilter(typeof(InboxConsumeFilter<>), context);
                });
            });

            services.Configure<OutboxPublisherOptions>(configuration.GetSection("Outbox"));
            services.Configure<MessageRetentionOptions>(configuration.GetSection("MessageRetention"));
            services.AddHostedService<OutboxPublisher>();
            services.AddHostedService<MessageRetentionPoller>();

            return services;
        }
    }
}
