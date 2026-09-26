using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ExpireDuePayments;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ProcessProviderCallback;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ReconcilePaymentIntent;
using AeroTech.JetPay.Mock.Clock;
using AeroTech.JetPay.Mock.Faults;
using AeroTech.JetPay.Mock.Funding;
using AeroTech.Messages.JetPay.Enums;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AeroTech.JetPay.Mock.Api
{
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Mock")]
    [Route($"Mock/v{{version:apiVersion}}")]
    public sealed class MockController : ControllerBase
    {
        private const int SweepBatchSize = 1_000;

        private readonly IMediator _mediator;
        private readonly MockFundingLedger _ledger;
        private readonly MockClock _clock;
        private readonly TransportFaultInjector _faults;

        public MockController(IMediator mediator, MockFundingLedger ledger, MockClock clock, TransportFaultInjector faults)
        {
            _mediator = mediator;
            _ledger = ledger;
            _clock = clock;
            _faults = faults;
        }

        [HttpGet("Funding")]
        public IActionResult GetFunding() => Ok(_ledger.Snapshot());

        [HttpDelete("Funding")]
        public IActionResult ResetFunding()
        {
            _ledger.Reset();
            return Ok();
        }

        [HttpPut("Funding/Wallets")]
        public IActionResult SetWallet([FromBody] SetWalletRequest request)
            => Ok(_ledger.SetWallet(request.PayerType, request.PayerId, request.CurrencyId, request.Code, request.Balance, request.IsDefault));

        [HttpGet("Funding/Wallets/{payerType}/{payerId:long}/{currencyId:int}")]
        public IActionResult GetWallets(PayerType payerType, long payerId, int currencyId)
            => Ok(_ledger.WalletsOf(payerType, payerId, currencyId));

        [HttpGet("Provider-Profiles")]
        public IActionResult GetProviderProfiles() => Ok(_ledger.Profiles());

        [HttpPut("Provider-Profiles/{providerProfileId}")]
        public IActionResult ConfigureProviderProfile(string providerProfileId, [FromBody] ConfigureProviderProfileRequest request)
            => Ok(_ledger.ConfigureProfile(providerProfileId, profile =>
            {
                profile.Enabled = request.Enabled ?? profile.Enabled;
                profile.Mode = request.Mode ?? profile.Mode;
                profile.CurrencyIds = request.CurrencyIds?.ToArray() ?? profile.CurrencyIds;
                profile.MinimumAmount = request.MinimumAmount ?? profile.MinimumAmount;
                profile.MaximumAmount = request.MaximumAmount ?? profile.MaximumAmount;
                profile.SupportsInquiry = request.SupportsInquiry ?? profile.SupportsInquiry;
                profile.RequiresSettlementAfterVerify = request.RequiresSettlementAfterVerify ?? profile.RequiresSettlementAfterVerify;
                profile.VerifyWindowSeconds = request.VerifyWindowSeconds ?? profile.VerifyWindowSeconds;
            }));

        [HttpGet("Pgw/{providerProfileId}/Pay/{paymentIntentId}")]
        public IActionResult PgwPaymentPage(string providerProfileId, string paymentIntentId)
            => Ok(new
            {
                providerProfileId,
                paymentIntentId,
                pay = $"POST /Mock/v1/Payment-Intents/{paymentIntentId}/Customer-Payment",
                callback = $"POST /Mock/v1/Payment-Intents/{paymentIntentId}/Callback",
                payAndReturn = $"POST /Mock/v1/Payment-Intents/{paymentIntentId}/Customer-Action/Complete"
            });

        [HttpPost("Payment-Intents/{paymentIntentId}/Customer-Payment")]
        public IActionResult PayAtGateway(string paymentIntentId, [FromBody] CustomerPaymentRequest? request)
            => _ledger.PayLatest(paymentIntentId, (request ?? new CustomerPaymentRequest()).Outcome, _clock.GetDateTime()) is { } transaction
                ? Ok(transaction)
                : NotFound();

        [HttpPost("Payment-Intents/{paymentIntentId}/Callback")]
        public async Task<IActionResult> Callback(string paymentIntentId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ProcessProviderCallbackCommand(paymentIntentId, PaidAtOf(paymentIntentId)), cancellationToken));

        [HttpPost("Payment-Intents/{paymentIntentId}/Customer-Action/Complete")]
        public async Task<IActionResult> CompleteCustomerAction(
            string paymentIntentId,
            [FromBody] CustomerPaymentRequest? request,
            CancellationToken cancellationToken)
        {
            _ledger.PayLatest(paymentIntentId, (request ?? new CustomerPaymentRequest()).Outcome, _clock.GetDateTime());
            return Ok(await _mediator.Send(new ProcessProviderCallbackCommand(paymentIntentId, PaidAtOf(paymentIntentId)), cancellationToken));
        }

        [HttpPost("Payment-Intents/{paymentIntentId}/Reconcile")]
        public async Task<IActionResult> Reconcile(string paymentIntentId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ReconcilePaymentIntentCommand(paymentIntentId), cancellationToken));

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

        [HttpPost("Jobs/Expire-Due-Payments")]
        public async Task<IActionResult> ExpireDuePayments(CancellationToken cancellationToken)
            => Ok(new { sweptSessions = await _mediator.Send(new ExpireDuePaymentsCommand(SweepBatchSize), cancellationToken) });

        [HttpPost("Faults/Transport-Timeout")]
        public IActionResult ArmTransportTimeout([FromBody] ArmTransportTimeoutRequest request)
        {
            _faults.ArmTransportTimeouts(request.Count);
            return Ok(new { pending = _faults.PendingTimeouts });
        }

        private DateTimeOffset? PaidAtOf(string paymentIntentId)
            => _ledger.OperationsOf(paymentIntentId).LastOrDefault()?.PaidAt;
    }
}
