using AeroTech.JetPay.Mock.Scenarios;

namespace AeroTech.JetPay.Mock.Api
{
    public sealed record BindScenarioRequest(MockScenario Scenario);

    public sealed record AdvanceClockRequest(int Seconds);

    public sealed record ArmTransportTimeoutRequest(int Count);
}
