using AeroTech.JetPay.RestApi.V1.PaymentMethodOptions.Requests;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.JetPay.RestApi.V1.PaymentMethodOptions
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Service2Service")]
    [Route($"Service/v{{version:apiVersion}}/Payment-Method-Options")]
    public sealed class ServiceController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ServiceController(IMediator mediator) => _mediator = mediator;

        [HttpPost("Resolve")]
        public async Task<IActionResult> Resolve([FromBody] ResolvePaymentMethodOptionsRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(request.ToQuery(), cancellationToken));
    }
}
