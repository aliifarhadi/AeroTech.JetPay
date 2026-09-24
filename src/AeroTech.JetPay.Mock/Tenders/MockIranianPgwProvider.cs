using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Tenders
{
    /// <summary>Iranian IPG: token, redirect, callback, then server-side verify before anything counts as paid.</summary>
    public sealed class MockIranianPgwProvider : MockTenderProvider
    {
        private static readonly MockScenario[] Scenarios =
        [
            MockScenario.IranianPgwRequiresActionThenCaptured,
            MockScenario.IranianPgwCaptured,
            MockScenario.IranianPgwFailed,
            MockScenario.PaymentProcessing,
            MockScenario.PaymentExpired,
            MockScenario.LateCapturedForSupersededInstruction
        ];

        public MockIranianPgwProvider(MockScenarioRegistry scenarios, IOptions<MockJetPayOptions> options, IClock clock)
            : base(scenarios, options, clock)
        {
        }

        public override TenderType TenderType => TenderType.IranianPgw;

        protected override MockScenario DefaultScenario => MockScenario.IranianPgwRequiresActionThenCaptured;

        protected override IReadOnlyCollection<MockScenario> SupportedScenarios => Scenarios;

        protected override TenderOutcome Start(MockScenario scenario, TenderStartRequest request) => scenario switch
        {
            MockScenario.IranianPgwCaptured => TenderOutcome.Captured(request.Amount),
            MockScenario.PaymentProcessing => TenderOutcome.Processing(),
            MockScenario.PaymentExpired => TenderOutcome.RequiresCustomerAction(
                Redirect(request.PaymentIntentId, request.IntentExpiresAt, Options.ExpiringCustomerActionTtlSeconds)),
            _ => TenderOutcome.RequiresCustomerAction(
                Redirect(request.PaymentIntentId, request.IntentExpiresAt, Options.CustomerActionTtlSeconds))
        };

        protected override TenderOutcome Verify(MockScenario scenario, TenderVerifyRequest request) => scenario switch
        {
            MockScenario.IranianPgwFailed => TenderOutcome.Failed("Declined", "The bank declined the payment during verification."),
            MockScenario.PaymentExpired => TenderOutcome.Failed("Expired", "The customer action expired before the payment was completed."),
            _ => TenderOutcome.Captured(request.Amount)
        };
    }
}
