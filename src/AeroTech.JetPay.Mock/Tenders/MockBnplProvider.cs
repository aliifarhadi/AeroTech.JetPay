using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Tenders
{
    /// <summary>BNPL: customer approval, then a provider commitment that is issuance guarantee only if the profile says so.</summary>
    public sealed class MockBnplProvider : MockTenderProvider
    {
        private static readonly MockScenario[] Scenarios =
        [
            MockScenario.BnplRequiresActionThenAuthorized,
            MockScenario.BnplRejected,
            MockScenario.PaymentProcessing,
            MockScenario.PaymentExpired
        ];

        public MockBnplProvider(MockScenarioRegistry scenarios, IOptions<MockJetPayOptions> options, IClock clock)
            : base(scenarios, options, clock)
        {
        }

        public override TenderType TenderType => TenderType.Bnpl;

        protected override MockScenario DefaultScenario => MockScenario.BnplRequiresActionThenAuthorized;

        protected override IReadOnlyCollection<MockScenario> SupportedScenarios => Scenarios;

        protected override TenderOutcome Start(MockScenario scenario, TenderStartRequest request) => scenario switch
        {
            MockScenario.PaymentProcessing => TenderOutcome.Processing(),
            MockScenario.PaymentExpired => TenderOutcome.RequiresCustomerAction(
                Redirect(request.PaymentIntentId, request.IntentExpiresAt, Options.ExpiringCustomerActionTtlSeconds)),
            _ => TenderOutcome.RequiresCustomerAction(
                Redirect(request.PaymentIntentId, request.IntentExpiresAt, Options.CustomerActionTtlSeconds))
        };

        protected override TenderOutcome Verify(MockScenario scenario, TenderVerifyRequest request) => scenario switch
        {
            MockScenario.BnplRejected => TenderOutcome.Failed("Declined", "The BNPL provider rejected the instalment plan."),
            MockScenario.PaymentExpired => TenderOutcome.Failed("Expired", "The customer action expired before the plan was approved."),
            _ => Authorized(request.Amount, Options.Bnpl, Options.Bnpl.AuthorizationValiditySeconds)
        };
    }
}
