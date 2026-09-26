using AeroTech.JetPay.Domain.Providers.Profiles;
using AeroTech.JetPay.Mock.Configuration;
using AeroTech.Messages.JetPay.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Funding
{
    public sealed class MockFundingLedger
    {
        public const string StoredValueProfile = "mock-stored-value";
        public const string PrimaryPgwProfile = "mock-pgw-a";
        public const string SecondaryPgwProfile = "mock-pgw-b";

        private readonly object _gate = new();
        private readonly MockJetPayOptions _options;
        private readonly Dictionary<string, MockWallet> _wallets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MockOperation> _operations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MockProviderProfileSettings> _profiles = new(StringComparer.Ordinal);
        private readonly List<MockRouteAttempt> _routeAttempts = [];

        public MockFundingLedger(IOptions<MockJetPayOptions> options)
        {
            _options = options.Value;
            Reset();
        }

        public void Reset()
        {
            lock (_gate)
            {
                _wallets.Clear();
                _operations.Clear();
                _profiles.Clear();
                _routeAttempts.Clear();

                _profiles[StoredValueProfile] = new MockProviderProfileSettings
                {
                    Id = StoredValueProfile,
                    TenderType = TenderType.StoredValue,
                    CurrencyIds = _options.StoredValueCurrencyIds,
                    SupportsInquiry = true,
                    SupportsProviderIdempotency = true,
                    UnverifiedPaymentExpiryBehavior = UnverifiedPaymentExpiryBehavior.ManualReconciliation
                };

                _profiles[PrimaryPgwProfile] = new MockProviderProfileSettings
                {
                    Id = PrimaryPgwProfile,
                    TenderType = TenderType.IranianPgw,
                    CurrencyIds = _options.PgwCurrencyIds,
                    SupportsInquiry = true,
                    RequiresSettlementAfterVerify = true,
                    SupportsProviderIdempotency = true,
                    VerifyWindowSeconds = _options.VerifyWindowSeconds
                };

                _profiles[SecondaryPgwProfile] = new MockProviderProfileSettings
                {
                    Id = SecondaryPgwProfile,
                    TenderType = TenderType.IranianPgw,
                    CurrencyIds = _options.PgwCurrencyIds,
                    VerifyWindowSeconds = _options.VerifyWindowSeconds
                };
            }
        }

        public MockWallet SetWallet(PayerType payerType, long payerId, int currencyId, string code, decimal balance, bool isDefault)
        {
            lock (_gate)
            {
                var id = $"wallet:{payerType}:{payerId}:{currencyId}:{code}";

                if (!_wallets.TryGetValue(id, out var wallet))
                    _wallets[id] = wallet = new MockWallet { Id = id, Code = code, PayerType = payerType, PayerId = payerId, CurrencyId = currencyId };

                wallet.Balance = balance;

                if (isDefault)
                {
                    foreach (var other in _wallets.Values.Where(other => other.PayerType == payerType && other.PayerId == payerId && other.CurrencyId == currencyId))
                        other.IsDefault = false;
                }

                wallet.IsDefault = isDefault;
                return Copy(wallet);
            }
        }

        public IReadOnlyList<MockWallet> WalletsOf(PayerType payerType, long payerId, int currencyId)
        {
            lock (_gate)
                return _wallets.Values
                    .Where(wallet => wallet.PayerType == payerType && wallet.PayerId == payerId && wallet.CurrencyId == currencyId)
                    .OrderBy(wallet => wallet.Code, StringComparer.Ordinal)
                    .Select(Copy)
                    .ToList();
        }

        public MockProviderProfileSettings ConfigureProfile(string providerProfileId, Action<MockProviderProfileSettings> configure)
        {
            lock (_gate)
            {
                if (!_profiles.TryGetValue(providerProfileId, out var profile))
                    throw new KeyNotFoundException($"Mock provider profile '{providerProfileId}' does not exist.");

                configure(profile);
                profile.Revision++;
                return Copy(profile);
            }
        }

        public IReadOnlyList<MockProviderProfileSettings> Profiles()
        {
            lock (_gate)
                return _profiles.Values.OrderBy(profile => profile.Id, StringComparer.Ordinal).Select(Copy).ToList();
        }

        public MockProviderProfileSettings? Profile(string providerProfileId)
        {
            lock (_gate)
                return _profiles.TryGetValue(providerProfileId, out var profile) ? Copy(profile) : null;
        }

        public MockOperation? FindOperation(string key)
        {
            lock (_gate)
                return _operations.TryGetValue(key, out var operation) ? Copy(operation) : null;
        }

        public IReadOnlyList<MockOperation> OperationsOf(string paymentIntentId)
        {
            lock (_gate)
                return _operations.Values
                    .Where(operation => operation.PaymentIntentId == paymentIntentId)
                    .OrderBy(operation => operation.CreatedAt)
                    .Select(Copy)
                    .ToList();
        }

        public IReadOnlyList<MockRouteAttempt> RouteAttempts
        {
            get
            {
                lock (_gate)
                    return _routeAttempts.ToList();
            }
        }

        public object Snapshot()
        {
            lock (_gate)
                return new
                {
                    wallets = _wallets.Values.Select(Copy).ToList(),
                    providerProfiles = _profiles.Values.OrderBy(profile => profile.Id, StringComparer.Ordinal).Select(Copy).ToList(),
                    operations = _operations.Values.Select(Copy).ToList(),
                    routeAttempts = _routeAttempts.ToList()
                };
        }

        public void RecordRouteAttempt(string paymentIntentId, string providerProfileId, string result)
        {
            lock (_gate)
                _routeAttempts.Add(new MockRouteAttempt(paymentIntentId, providerProfileId, result));
        }

        public (MockOperation? Operation, string? FailureCode) DebitWallet(string key, string paymentIntentId, string walletId, decimal amount, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (_operations.TryGetValue(key, out var existing))
                {
                    existing.StartCalls++;
                    return (Copy(existing), null);
                }

                if (!_wallets.TryGetValue(walletId, out var wallet))
                    return (null, "FundingSourceUnavailable");

                if (wallet.Balance < amount)
                    return (null, "InsufficientFunds");

                wallet.Balance -= amount;

                var debit = Record(key, MockOperationKind.WalletDebit, walletId, StoredValueProfile, paymentIntentId, amount, now);
                debit.CustomerOutcome = MockCustomerOutcome.Paid;
                debit.PaidAt = now;
                debit.VerifiedAt = now;
                return (Copy(debit), null);
            }
        }

        public MockOperation OpenPgwTransaction(string key, string providerProfileId, string paymentIntentId, decimal amount, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (_operations.TryGetValue(key, out var existing))
                {
                    existing.StartCalls++;
                    return Copy(existing);
                }

                return Copy(Record(key, MockOperationKind.PgwTransaction, $"pgw:{providerProfileId}:{Guid.NewGuid():N}", providerProfileId, paymentIntentId, amount, now));
            }
        }

        public MockOperation? PayLatest(string paymentIntentId, MockCustomerOutcome outcome, DateTimeOffset now)
        {
            lock (_gate)
            {
                var transaction = _operations.Values
                    .Where(operation => operation.Kind == MockOperationKind.PgwTransaction && operation.PaymentIntentId == paymentIntentId)
                    .OrderByDescending(operation => operation.CreatedAt)
                    .FirstOrDefault();

                if (transaction is null)
                    return null;

                if (transaction.CustomerOutcome is null)
                {
                    transaction.CustomerOutcome = outcome;
                    transaction.PaidAt = outcome == MockCustomerOutcome.Paid ? now : null;
                }

                return Copy(transaction);
            }
        }

        public MockOperation? VerifyPgw(string key, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (!_operations.TryGetValue(key, out var transaction))
                    return null;

                ReturnIfWindowElapsed(transaction, now);

                if (transaction is { CustomerOutcome: MockCustomerOutcome.Paid, ReturnedAt: null })
                    transaction.VerifiedAt ??= now;

                return Copy(transaction);
            }
        }

        public MockOperation? SettlePgw(string key, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (!_operations.TryGetValue(key, out var transaction))
                    return null;

                if (transaction.VerifiedAt is not null)
                    transaction.SettledAt ??= now;

                return Copy(transaction);
            }
        }

        public MockOperation? InquirePgw(string key, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (!_operations.TryGetValue(key, out var transaction))
                    return null;

                ReturnIfWindowElapsed(transaction, now);
                return Copy(transaction);
            }
        }

        private void ReturnIfWindowElapsed(MockOperation transaction, DateTimeOffset now)
        {
            if (transaction is not { CustomerOutcome: MockCustomerOutcome.Paid, VerifiedAt: null, ReturnedAt: null, PaidAt: { } paidAt })
                return;

            if (_profiles.GetValueOrDefault(transaction.ProviderProfileId)?.VerifyWindowSeconds is not { } window)
                return;

            var deadline = paidAt.AddSeconds(window);

            if (now > deadline)
                transaction.ReturnedAt = deadline;
        }

        private MockOperation Record(
            string key,
            MockOperationKind kind,
            string reference,
            string providerProfileId,
            string paymentIntentId,
            decimal amount,
            DateTimeOffset now)
            => _operations[key] = new MockOperation
            {
                Key = key,
                Kind = kind,
                Reference = reference,
                ProviderProfileId = providerProfileId,
                PaymentIntentId = paymentIntentId,
                Amount = amount,
                CreatedAt = now
            };

        private static MockWallet Copy(MockWallet wallet)
            => new() { Id = wallet.Id, Code = wallet.Code, PayerType = wallet.PayerType, PayerId = wallet.PayerId, CurrencyId = wallet.CurrencyId, Balance = wallet.Balance, IsDefault = wallet.IsDefault };

        private static MockOperation Copy(MockOperation operation)
            => new()
            {
                Key = operation.Key,
                Kind = operation.Kind,
                Reference = operation.Reference,
                ProviderProfileId = operation.ProviderProfileId,
                PaymentIntentId = operation.PaymentIntentId,
                Amount = operation.Amount,
                CreatedAt = operation.CreatedAt,
                CustomerOutcome = operation.CustomerOutcome,
                PaidAt = operation.PaidAt,
                VerifiedAt = operation.VerifiedAt,
                SettledAt = operation.SettledAt,
                ReturnedAt = operation.ReturnedAt,
                StartCalls = operation.StartCalls
            };

        private static MockProviderProfileSettings Copy(MockProviderProfileSettings profile)
            => new()
            {
                Id = profile.Id,
                TenderType = profile.TenderType,
                Revision = profile.Revision,
                Enabled = profile.Enabled,
                Mode = profile.Mode,
                CurrencyIds = profile.CurrencyIds.ToArray(),
                MinimumAmount = profile.MinimumAmount,
                MaximumAmount = profile.MaximumAmount,
                SupportsInquiry = profile.SupportsInquiry,
                RequiresSettlementAfterVerify = profile.RequiresSettlementAfterVerify,
                SupportsProviderIdempotency = profile.SupportsProviderIdempotency,
                UnverifiedPaymentExpiryBehavior = profile.UnverifiedPaymentExpiryBehavior,
                VerifyWindowSeconds = profile.VerifyWindowSeconds
            };
    }
}
