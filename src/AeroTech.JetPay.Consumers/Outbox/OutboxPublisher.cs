using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Consumers.Outbox
{
    public sealed class OutboxPublisher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBus _bus;
        private readonly OutboxPublisherOptions _options;
        private readonly ILogger<OutboxPublisher> _logger;

        public OutboxPublisher(
            IServiceScopeFactory scopeFactory,
            IBus bus,
            IOptions<OutboxPublisherOptions> options,
            ILogger<OutboxPublisher> logger)
        {
            _scopeFactory = scopeFactory;
            _bus = bus;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PublishIntervalSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishPendingAsync(stoppingToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Outbox publishing loop failed.");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        private async Task PublishPendingAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<JetPayDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var pending = await dbContext.OutboxMessages
                .Where(message => message.ProcessedOn == null)
                .OrderBy(message => message.Id)
                .Take(_options.BatchSize)
                .ToListAsync(cancellationToken);

            if (pending.Count == 0)
                return;

            foreach (var message in pending)
            {
                var messageType = Type.GetType(message.MessageType);
                if (messageType is null)
                {
                    _logger.LogError("Unable to resolve outbox message type '{MessageType}' (Id {Id}).", message.MessageType, message.Id);
                    continue;
                }

                var payload = JsonSerializer.Deserialize(message.Payload, messageType);
                if (payload is null)
                    continue;

                var messageId = OutboxMessageIdentity.Of(payload);
                if (messageId is null)
                {
                    _logger.LogError(
                        "Outbox message {Id} ({MessageType}) has no usable EventId/CommandId; cannot derive a stable MessageId.",
                        message.Id,
                        message.MessageType);
                    continue;
                }

                try
                {
                    await _bus.Publish(payload, messageType, Pipe.Execute<PublishContext>(publish => publish.MessageId = messageId.Value), cancellationToken);

                    message.ProcessedOn = clock.GetDateTime();
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to publish outbox message {Id} ({MessageType}).", message.Id, message.MessageType);
                }
            }
        }
    }
}
