using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AeroTech.JetPay.Application._Shared.Idempotency
{
    /// <summary>
    /// Canonical hash of an idempotent request's business payload. Decimals are normalized so 100 and 100.00 are the
    /// same amount, and instants are compared in UTC.
    /// </summary>
    public static class RequestFingerprint
    {
        private const char Separator = '\u001f';
        private const string NullToken = "∅";
        private const decimal NormalizingDivisor = 1.000000000000000000000000000000000m;

        public static string Of(params object?[] parts)
        {
            var canonical = string.Join(Separator, parts.Select(Canonical));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }

        private static string Canonical(object? part) => part switch
        {
            null => NullToken,
            decimal amount => (amount / NormalizingDivisor).ToString(CultureInfo.InvariantCulture),
            DateTimeOffset instant => instant.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => part.ToString() ?? string.Empty
        };
    }
}
