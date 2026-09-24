using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.JetPay.Mock.Clock
{
    /// <summary>
    /// Mock-only clock that can be moved forward so expiry paths are reachable deterministically without waiting.
    /// </summary>
    public sealed class MockClock : IClock
    {
        private long _offsetTicks;

        public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

        public DateTimeOffset GetDateTime() => DateTimeOffset.UtcNow + Offset;

        public DateOnly GetDate() => DateOnly.FromDateTime(GetDateTime().UtcDateTime);

        public void Advance(TimeSpan by)
        {
            if (by < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(by), "The mock clock only moves forward.");

            Interlocked.Add(ref _offsetTicks, by.Ticks);
        }

        public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
    }
}
