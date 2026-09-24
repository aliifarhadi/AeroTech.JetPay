namespace AeroTech.JetPay.Mock.Faults
{
    public sealed class TransportFaultInjector
    {
        private int _pendingTimeouts;

        public int PendingTimeouts => Volatile.Read(ref _pendingTimeouts);

        public void ArmTransportTimeouts(int count) => Interlocked.Exchange(ref _pendingTimeouts, Math.Max(0, count));

        public bool TryConsumeTransportTimeout()
        {
            while (true)
            {
                var pending = Volatile.Read(ref _pendingTimeouts);

                if (pending <= 0)
                    return false;

                if (Interlocked.CompareExchange(ref _pendingTimeouts, pending - 1, pending) == pending)
                    return true;
            }
        }
    }
}
