using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects;
using AeroTech.JetPay.Domain.Providers.Tenders;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.JetPay.Mock.Scenarios;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Tenders
{
    /// <summary>
    /// Deterministic stand-in for a tender adapter: the bound <see cref="MockScenario"/> decides every outcome and no
    /// external provider is ever called. Capture and release always succeed; Stage 3 defines no failing variant.
    /// </summary>
    public abstract class MockTenderProvider : ITenderProvider
    {
        private readonly MockScenarioRegistry _scenarios;

        protected MockTenderProvider(MockScenarioRegistry scenarios, IOptions<MockJetPayOptions> options, IClock clock)
        {
            _scenarios = scenarios;
            Options = options.Value;
            Clock = clock;
        }

        public abstract TenderType TenderType { get; }

        protected abstract MockScenario DefaultScenario { get; }

        protected abstract IReadOnlyCollection<MockScenario> SupportedScenarios { get; }

        protected MockJetPayOptions Options { get; }

        protected IClock Clock { get; }

        public Task<TenderOutcome> StartAsync(TenderStartRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Start(ScenarioFor(request.PaymentIntentId, request.OrderId), request));

        public Task<TenderOutcome> VerifyAsync(TenderVerifyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Verify(ScenarioFor(request.PaymentIntentId, request.OrderId), request));

        public Task<TenderOutcome> CaptureAsync(TenderCaptureRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenderOutcome.Captured(request.Amount));

        public Task<TenderOutcome> ReleaseAsync(TenderReleaseRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenderOutcome.Released());

        protected abstract TenderOutcome Start(MockScenario scenario, TenderStartRequest request);

        protected abstract TenderOutcome Verify(MockScenario scenario, TenderVerifyRequest request);

        protected CustomerAction Redirect(string paymentIntentId, DateTimeOffset? intentExpiresAt, int ttlSeconds)
        {
            var actionExpiresAt = Clock.GetDateTime().AddSeconds(ttlSeconds);

            if (intentExpiresAt < actionExpiresAt)
                actionExpiresAt = intentExpiresAt.Value;

            return CustomerAction.Redirect(
                $"{Options.PublicBaseUrl.TrimEnd('/')}/Mock/v1/Customer-Actions/{Uri.EscapeDataString(paymentIntentId)}",
                actionExpiresAt);
        }

        protected TenderOutcome Authorized(decimal amount, MockTenderProfileOptions profile, int? validitySeconds)
            => TenderOutcome.Authorized(
                amount,
                profile.SupportsIssuanceGuaranteeOnAuthorization,
                validitySeconds is { } seconds ? Clock.GetDateTime().AddSeconds(seconds) : null);

        private MockScenario ScenarioFor(string paymentIntentId, long orderId)
        {
            var scenario = _scenarios.Find(paymentIntentId, orderId) ?? DefaultScenario;

            return SupportedScenarios.Contains(scenario)
                ? scenario
                : throw MockExceptions.ScenarioNotApplicable(scenario, TenderType);
        }
    }
}
