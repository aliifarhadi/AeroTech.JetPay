using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.ExpireDuePayments;
using AeroTech.JetPay.Application.PaymentSessionAggregate.Commands.VerifyPaymentIntent;
using AeroTech.JetPay.Mock.Clock;
using AeroTech.JetPay.Mock.Faults;
using AeroTech.JetPay.Mock.Funding;
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
        public IActionResult GetWallets(Messages.JetPay.Enums.PayerType payerType, long payerId, int currencyId)
            => Ok(_ledger.WalletsOf(payerType, payerId, currencyId));

        [HttpPut("Funding/Credit-Facilities")]
        public IActionResult SetCreditFacility([FromBody] SetCreditFacilityRequest request)
            => Ok(_ledger.SetCreditFacility(request.TenderType, request.PayerType, request.PayerId, request.CurrencyId, request.Limit, request.AuthorizationValiditySeconds));

        [HttpPut("Funding/Cash")]
        public IActionResult SetCashAcceptance([FromBody] SetCashAcceptanceRequest request)
        {
            _ledger.SetCashAcceptance(request.OfficeIds, request.SalesChannels ?? []);
            return Ok();
        }

        [HttpPut("Funding/Bnpl")]
        public IActionResult SetBnpl([FromBody] SetBnplRequest request)
        {
            _ledger.SetBnpl(
                request.Enabled,
                request.MinimumAmount,
                request.MaximumAmount,
                (request.DisabledPayers ?? []).Select(payer => (payer.PayerType, payer.PayerId)));
            return Ok();
        }

        [HttpPut("Funding/Pgw-Profiles/{code}")]
        public IActionResult ConfigurePgwProfile(string code, [FromBody] ConfigurePgwProfileRequest request)
            => Ok(_ledger.ConfigurePgwProfile(code, profile =>
            {
                profile.Priority = request.Priority ?? profile.Priority;
                profile.Enabled = request.Enabled ?? profile.Enabled;
                profile.CurrencyIds = request.CurrencyIds?.ToArray() ?? profile.CurrencyIds;
                profile.Mode = request.Mode ?? profile.Mode;
                profile.InquiryOutcome = request.InquiryOutcome ?? profile.InquiryOutcome;
                profile.MinimumAmount = request.MinimumAmount ?? profile.MinimumAmount;
                profile.MaximumAmount = request.MaximumAmount ?? profile.MaximumAmount;
            }));

        [HttpPost("Payment-Intents/{paymentIntentId}/Customer-Action/Complete")]
        public async Task<IActionResult> CompleteCustomerAction(
            string paymentIntentId,
            [FromBody] CompleteCustomerActionRequest? request,
            CancellationToken cancellationToken)
        {
            _ledger.SetCustomerOutcome(paymentIntentId, (request ?? new CompleteCustomerActionRequest()).Outcome);
            return Ok(await _mediator.Send(new VerifyPaymentIntentCommand(paymentIntentId), cancellationToken));
        }

        [HttpPost("Payment-Intents/{paymentIntentId}/Reconcile")]
        public async Task<IActionResult> Reconcile(string paymentIntentId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new VerifyPaymentIntentCommand(paymentIntentId), cancellationToken));

        [HttpGet("Pgw/{routeCode}/Pay/{paymentIntentId}")]
        public IActionResult PgwPaymentPage(string routeCode, string paymentIntentId)
            => Ok(new { routeCode, paymentIntentId, complete = $"POST /Mock/v1/Payment-Intents/{paymentIntentId}/Customer-Action/Complete" });

        [HttpGet("Bnpl/Apply/{paymentIntentId}")]
        public IActionResult BnplApplicationPage(string paymentIntentId)
            => Ok(new { paymentIntentId, complete = $"POST /Mock/v1/Payment-Intents/{paymentIntentId}/Customer-Action/Complete" });

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
            => Ok(new { changedSessions = await _mediator.Send(new ExpireDuePaymentsCommand(SweepBatchSize), cancellationToken) });

        [HttpPost("Faults/Transport-Timeout")]
        public IActionResult ArmTransportTimeout([FromBody] ArmTransportTimeoutRequest request)
        {
            _faults.ArmTransportTimeouts(request.Count);
            return Ok(new { pending = _faults.PendingTimeouts });
        }
    }
}
