using System.Collections.Concurrent;

namespace AeroTech.JetPay.Mock.Scenarios
{
    /// <summary>
    /// Mock-only scenario bindings. A payment-intent binding wins over an order binding; with neither, each tender
    /// follows its default success path. Bindings live in memory and reset with the process.
    /// </summary>
    public sealed class MockScenarioRegistry
    {
        private readonly ConcurrentDictionary<long, MockScenario> _byOrder = new();
        private readonly ConcurrentDictionary<string, MockScenario> _byPaymentIntent = new(StringComparer.Ordinal);

        public void BindOrder(long orderId, MockScenario scenario) => _byOrder[orderId] = scenario;

        public void BindPaymentIntent(string paymentIntentId, MockScenario scenario) => _byPaymentIntent[paymentIntentId] = scenario;

        public void Clear()
        {
            _byOrder.Clear();
            _byPaymentIntent.Clear();
        }

        public MockScenario? Find(string paymentIntentId, long orderId)
        {
            if (_byPaymentIntent.TryGetValue(paymentIntentId, out var intentScenario))
                return intentScenario;

            return _byOrder.TryGetValue(orderId, out var orderScenario) ? orderScenario : null;
        }
    }
}
