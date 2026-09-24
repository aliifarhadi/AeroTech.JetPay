using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.ExpireDuePaymentIntents;
using AeroTech.JetPay.Application.PaymentIntentAggregate.Commands.VerifyPaymentIntent;
using AeroTech.JetPay.Mock.Clock;
using AeroTech.JetPay.Mock.Faults;
using AeroTech.JetPay.Mock.Scenarios;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.JetPay.Mock.Api
{
    /// <summary>
    /// Mock-only test control surface. It exists only when MockJetPay is enabled and is not part of the JetPay
    /// service contract.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Mock")]
    [Route($"Mock/v{{version:apiVersion}}")]
    public sealed class MockController : ControllerBase
    {
        private const int SweepBatchSize = 1_000;

        private readonly IMediator _mediator;
        private readonly MockScenarioRegistry _scenarios;
        private readonly MockClock _clock;
        private readonly TransportFaultInjector _faults;

        public MockController(IMediator mediator, MockScenarioRegistry scenarios, MockClock clock, TransportFaultInjector faults)
        {
            _mediator = mediator;
            _scenarios = scenarios;
            _clock = clock;
            _faults = faults;
        }

        [HttpPut("Scenarios/Orders/{orderId:long}")]
        public IActionResult BindOrderScenario(long orderId, [FromBody] BindScenarioRequest request)
        {
            _scenarios.BindOrder(orderId, request.Scenario);
            return Ok(new { orderId, request.Scenario });
        }

        [HttpPut("Scenarios/Payment-Intents/{paymentIntentId}")]
        public IActionResult BindPaymentIntentScenario(string paymentIntentId, [FromBody] BindScenarioRequest request)
        {
            _scenarios.BindPaymentIntent(paymentIntentId, request.Scenario);
            return Ok(new { paymentIntentId, request.Scenario });
        }

        [HttpDelete("Scenarios")]
        public IActionResult ClearScenarios()
        {
            _scenarios.Clear();
            return Ok();
        }

        /// <summary>The page a Redirect customer action points at; completing it is a separate, explicit call.</summary>
        [HttpGet("Customer-Actions/{paymentIntentId}")]
        public IActionResult CustomerAction(string paymentIntentId)
            => Ok(new
            {
                paymentIntentId,
                complete = $"POST /Mock/v1/Payment-Intents/{paymentIntentId}/Customer-Action/Complete"
            });

        /// <summary>Simulates the provider callback; JetPay then verifies server-side before recording any outcome.</summary>
        [HttpPost("Payment-Intents/{paymentIntentId}/Customer-Action/Complete")]
        public async Task<IActionResult> CompleteCustomerAction(string paymentIntentId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new VerifyPaymentIntentCommand(paymentIntentId), cancellationToken));

        [HttpGet("Clock")]
        public IActionResult GetClock() => Ok(new { now = _clock.GetDateTime(), offsetSeconds = _clock.Offset.TotalSeconds });

        [HttpPost("Clock/Advance")]
        public IActionResult AdvanceClock([FromBody] AdvanceClockRequest request)
        {
            _clock.Advance(TimeSpan.FromSeconds(request.Seconds));
            return GetClock();
        }

        [HttpDelete("Clock")]
        public IActionResult ResetClock()
        {
            _clock.Reset();
            return GetClock();
        }

        [HttpPost("Jobs/Expire-Due-Payment-Intents")]
        public async Task<IActionResult> ExpireDuePaymentIntents(CancellationToken cancellationToken)
            => Ok(new { expired = await _mediator.Send(new ExpireDuePaymentIntentsCommand(SweepBatchSize), cancellationToken) });

        [HttpPost("Faults/Transport-Timeout")]
        public IActionResult ArmTransportTimeout([FromBody] ArmTransportTimeoutRequest request)
        {
            _faults.ArmTransportTimeouts(request.Count);
            return Ok(new { pending = _faults.PendingTimeouts });
        }
    }
}
