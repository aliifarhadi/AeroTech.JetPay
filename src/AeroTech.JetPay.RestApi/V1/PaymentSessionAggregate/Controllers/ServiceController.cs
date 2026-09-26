using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.CancelPaymentSession;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Queries.GetPaymentSessionById;
using AeroTech.JetPay.RestApi.V1.PaymentSessionAggregate.Requests;
using AeroTech.JetPay.RestApi.V1._Shared;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.JetPay.RestApi.V1.PaymentSessionAggregate
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Service2Service")]
    [Route($"Service/v{{version:apiVersion}}/Payment-Sessions")]
    public sealed class ServiceController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ServiceController(IMediator mediator) => _mediator = mediator;

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
            [FromBody] CreatePaymentSessionRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(request.ToCommand(idempotencyKey ?? string.Empty), cancellationToken));

        [HttpGet("{paymentSessionId}")]
        public async Task<IActionResult> Get(string paymentSessionId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPaymentSessionByIdQuery(paymentSessionId), cancellationToken));

        [HttpPost("{paymentSessionId}/Selections")]
        public async Task<IActionResult> AddSelections(
            string paymentSessionId,
            [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
            [FromBody] AddPaymentSelectionsRequest request,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(request.ToCommand(idempotencyKey ?? string.Empty, paymentSessionId), cancellationToken));

        [HttpPost("{paymentSessionId}/Cancel")]
        public async Task<IActionResult> Cancel(
            string paymentSessionId,
            [FromHeader(Name = IdempotencyHeader.Name)] string? idempotencyKey,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new CancelPaymentSessionCommand(idempotencyKey ?? string.Empty, paymentSessionId), cancellationToken));
    }
}
