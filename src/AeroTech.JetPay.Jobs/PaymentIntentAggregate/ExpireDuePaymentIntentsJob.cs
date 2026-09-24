using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ExpireDuePaymentIntents;
using MediatR;
using Microsoft.Extensions.Options;
using Quartz;

namespace AeroTech.JetPay.Jobs.PaymentIntentAggregate
{
    [DisallowConcurrentExecution]
    public sealed class ExpireDuePaymentIntentsJob : IJob
    {
        private readonly ISender _sender;
        private readonly PaymentIntentExpiryOptions _options;

        public ExpireDuePaymentIntentsJob(ISender sender, IOptions<PaymentIntentExpiryOptions> options)
        {
            _sender = sender;
            _options = options.Value;
        }

        public Task Execute(IJobExecutionContext context)
            => _sender.Send(new ExpireDuePaymentIntentsCommand(_options.MaxBatch), context.CancellationToken);
    }
}
