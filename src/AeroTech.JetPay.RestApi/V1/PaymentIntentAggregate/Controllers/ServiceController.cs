using AeroTech.JetPay.Application.PaymentIntentAggregate.Queries.GetPaymentIntentById;
using AeroTech.JetPay.RestApi.V1.PaymentIntentAggregate.Requests;
using AeroTech.JetPay.RestApi.V1._Shared;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.JetPay.RestApi.V1.PaymentIntentAggregate
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Service2Service")]
    [Route($"Service/v{{version:apiVersion}}/Payment-Intents")]
    public sealed class ServiceController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ServiceController(IMediator mediator) => _mediator = mediator;

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
            [FromBody] CreatePaymentIntentRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(request.ToCommand(idempotencyKey ?? string.Empty), cancellationToken));

        [HttpGet("{paymentIntentId}")]
        public async Task<IActionResult> Get(string paymentIntentId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPaymentIntentByIdQuery(paymentIntentId), cancellationToken));

        [HttpPost("{paymentIntentId}/Confirm")]
        public async Task<IActionResult> Confirm(
            string paymentIntentId,
            [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
            [FromBody] ConfirmPaymentIntentRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(request.ToCommand(idempotencyKey ?? string.Empty, paymentIntentId), cancellationToken));

        [HttpPost("{paymentIntentId}/Capture")]
        public async Task<IActionResult> Capture(
            string paymentIntentId,
            [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
            [FromBody] CapturePaymentIntentRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(request.ToCommand(idempotencyKey ?? string.Empty, paymentIntentId), cancellationToken));

        [HttpPost("{paymentIntentId}/Cancel")]
        public async Task<IActionResult> Cancel(
            string paymentIntentId,
            [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
            [FromBody] CancelPaymentIntentRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(request.ToCommand(idempotencyKey ?? string.Empty, paymentIntentId), cancellationToken));
    }
}
