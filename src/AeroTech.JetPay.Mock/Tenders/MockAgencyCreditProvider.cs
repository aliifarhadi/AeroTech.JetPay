using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Tenders
{
    /// <summary>Agency credit: reserves available exposure; authorization is a commitment to pay, never captured money.</summary>
    public sealed class MockAgencyCreditProvider : MockTenderProvider
    {
        private static readonly MockScenario[] Scenarios =
        [
            MockScenario.AgencyCreditAuthorized,
            MockScenario.AgencyCreditInsufficientLimit,
            MockScenario.PaymentProcessing,
            MockScenario.PaymentExpired
        ];

        public MockAgencyCreditProvider(MockScenarioRegistry scenarios, IOptions<MockJetPayOptions> options, IClock clock)
            : base(scenarios, options, clock)
        {
        }

        public override TenderType TenderType => TenderType.AgencyCredit;

        protected override MockScenario DefaultScenario => MockScenario.AgencyCreditAuthorized;

        protected override IReadOnlyCollection<MockScenario> SupportedScenarios => Scenarios;

        protected override TenderOutcome Start(MockScenario scenario, TenderStartRequest request) => scenario switch
        {
            MockScenario.AgencyCreditInsufficientLimit => TenderOutcome.Failed("InsufficientFunds", "The agency credit line does not have enough available limit."),
            MockScenario.PaymentProcessing => TenderOutcome.Processing(),
            MockScenario.PaymentExpired => Authorized(request.Amount, Options.AgencyCredit, Options.ExpiringAuthorizationValiditySeconds),
            _ => Authorized(request.Amount, Options.AgencyCredit, Options.AgencyCredit.AuthorizationValiditySeconds)
        };

        protected override TenderOutcome Verify(MockScenario scenario, TenderVerifyRequest request)
            => Authorized(request.Amount, Options.AgencyCredit, Options.AgencyCredit.AuthorizationValiditySeconds);
    }
}
