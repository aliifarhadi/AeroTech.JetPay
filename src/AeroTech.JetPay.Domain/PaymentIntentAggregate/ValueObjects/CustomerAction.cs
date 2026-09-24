using AeroTech.Framework.Core.Domain.ValueObjects;
using AeroTech.Messages.JetPay.Enums;

namespace AeroTech.JetPay.Domain.PaymentIntentAggregate.ValueObjects
{
    public sealed class CustomerAction : ValueObject
    {
        private CustomerAction()
        {
        }

        public CustomerAction(
            CustomerActionType type,
            string? url,
            string? httpMethod,
            IReadOnlyDictionary<string, string>? formFields,
            DateTimeOffset? expiresAt)
        {
            Type = type;
            Url = url;
            HttpMethod = httpMethod;
            FormFields = formFields;
            ExpiresAt = expiresAt;
        }

        public CustomerActionType Type { get; private set; }

        public string? Url { get; private set; }

        public string? HttpMethod { get; private set; }

        public IReadOnlyDictionary<string, string>? FormFields { get; private set; }

        public DateTimeOffset? ExpiresAt { get; private set; }

        public static CustomerAction Redirect(string url, DateTimeOffset? expiresAt)
            => new(CustomerActionType.Redirect, url, "GET", null, expiresAt);

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Type;
            yield return Url;
            yield return HttpMethod;
            yield return ExpiresAt;

            foreach (var field in FormFields?.OrderBy(pair => pair.Key, StringComparer.Ordinal) ?? Enumerable.Empty<KeyValuePair<string, string>>())
                yield return field;
        }
    }
}
