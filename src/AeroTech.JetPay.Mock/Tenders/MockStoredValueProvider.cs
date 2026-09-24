using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Tenders
{
    /// <summary>Stored value: full-amount immediate debit, no customer redirect.</summary>
    public sealed class MockStoredValueProvider : MockTenderProvider
    {
        private static readonly MockScenario[] Scenarios =
        [
            MockScenario.StoredValueCaptured,
            MockScenario.StoredValueInsufficientBalance,
            MockScenario.PaymentProcessing
        ];

        public MockStoredValueProvider(MockScenarioRegistry scenarios, IOptions<MockJetPayOptions> options, IClock clock)
            : base(scenarios, options, clock)
        {
        }

        public override TenderType TenderType => TenderType.StoredValue;

        protected override MockScenario DefaultScenario => MockScenario.StoredValueCaptured;

        protected override IReadOnlyCollection<MockScenario> SupportedScenarios => Scenarios;

        protected override TenderOutcome Start(MockScenario scenario, TenderStartRequest request) => scenario switch
        {
            MockScenario.StoredValueInsufficientBalance => TenderOutcome.Failed("InsufficientFunds", "The stored-value balance does not cover the amount."),
            MockScenario.PaymentProcessing => TenderOutcome.Processing(),
            _ => TenderOutcome.Captured(request.Amount)
        };

        protected override TenderOutcome Verify(MockScenario scenario, TenderVerifyRequest request)
            => TenderOutcome.Captured(request.Amount);
    }
}
