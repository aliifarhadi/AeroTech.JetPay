using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ExpireDuePayments;
using MediatR;
using Microsoft.Extensions.Options;
using Quartz;

namespace AeroTech.JetPay.Jobs.PaymentSessionAggregate
{
    [DisallowConcurrentExecution]
    public sealed class ExpireDuePaymentsJob : IJob
    {
        private readonly ISender _sender;
        private readonly PaymentExpiryOptions _options;

        public ExpireDuePaymentsJob(ISender sender, IOptions<PaymentExpiryOptions> options)
        {
            _sender = sender;
            _options = options.Value;
        }

        public Task Execute(IJobExecutionContext context)
            => _sender.Send(new ExpireDuePaymentsCommand(_options.MaxBatch), context.CancellationToken);
    }
}
