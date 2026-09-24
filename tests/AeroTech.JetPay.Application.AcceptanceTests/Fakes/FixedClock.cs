using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.JetPay.Application.AcceptanceTests.Fakes;

public sealed class FixedClock : IClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    public DateTimeOffset GetDateTime() => Now;

    public DateOnly GetDate() => DateOnly.FromDateTime(Now.UtcDateTime);

    public void Advance(TimeSpan by) => Now += by;
}
