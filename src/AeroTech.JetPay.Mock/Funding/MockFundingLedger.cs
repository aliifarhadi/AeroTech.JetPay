using AeroTech.JetPay.Mock.Configuration;
using AeroTech.Messages.JetPay.Enums;
using AeroTech.Messages.Shared.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.JetPay.Mock.Funding
{
    public sealed class MockFundingLedger
    {
        public const string PrimaryPgwRoute = "mock-pgw-a";
        public const string SecondaryPgwRoute = "mock-pgw-b";

        private readonly object _gate = new();
        private readonly MockJetPayOptions _options;
        private readonly Dictionary<string, MockWallet> _wallets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MockCreditFacility> _facilities = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MockOperation> _operations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MockPgwProfile> _pgwProfiles = new(StringComparer.Ordinal);
        private readonly List<MockRouteAttempt> _routeAttempts = [];
        private readonly HashSet<long> _cashOffices = [];
        private readonly HashSet<SalesChannel> _cashChannels = [];
        private readonly HashSet<(PayerType, long)> _bnplDisabledPayers = [];

        public MockFundingLedger(IOptions<MockJetPayOptions> options)
        {
            _options = options.Value;
            Reset();
        }

        public bool BnplEnabled { get; private set; }

        public decimal? BnplMinimumAmount { get; private set; }

        public decimal? BnplMaximumAmount { get; private set; }

        public void Reset()
        {
            lock (_gate)
            {
                _wallets.Clear();
                _facilities.Clear();
                _operations.Clear();
                _pgwProfiles.Clear();
                _routeAttempts.Clear();
                _cashOffices.Clear();
                _cashChannels.Clear();
                _bnplDisabledPayers.Clear();

                _pgwProfiles[PrimaryPgwRoute] = new MockPgwProfile { Code = PrimaryPgwRoute, Priority = 1, CurrencyIds = _options.PgwCurrencyIds };
                _pgwProfiles[SecondaryPgwRoute] = new MockPgwProfile { Code = SecondaryPgwRoute, Priority = 2, CurrencyIds = _options.PgwCurrencyIds };

                BnplEnabled = true;
                BnplMinimumAmount = null;
                BnplMaximumAmount = null;
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
                return wallet;
            }
        }

        public MockCreditFacility SetCreditFacility(TenderType tenderType, PayerType payerType, long payerId, int currencyId, decimal limit, int? authorizationValiditySeconds)
        {
            lock (_gate)
            {
                var id = $"credit:{tenderType}:{payerType}:{payerId}:{currencyId}";

                if (!_facilities.TryGetValue(id, out var facility))
                    _facilities[id] = facility = new MockCreditFacility { Id = id, TenderType = tenderType, PayerType = payerType, PayerId = payerId, CurrencyId = currencyId };

                facility.Limit = limit;
                facility.AuthorizationValiditySeconds = authorizationValiditySeconds;
                return facility;
            }
        }

        public void SetCashAcceptance(IEnumerable<long> officeIds, IEnumerable<SalesChannel> salesChannels)
        {
            lock (_gate)
            {
                _cashOffices.Clear();
                _cashOffices.UnionWith(officeIds);
                _cashChannels.Clear();
                _cashChannels.UnionWith(salesChannels);
            }
        }

        public void SetBnpl(bool enabled, decimal? minimumAmount, decimal? maximumAmount, IEnumerable<(PayerType, long)> disabledPayers)
        {
            lock (_gate)
            {
                BnplEnabled = enabled;
                BnplMinimumAmount = minimumAmount;
                BnplMaximumAmount = maximumAmount;
                _bnplDisabledPayers.Clear();
                _bnplDisabledPayers.UnionWith(disabledPayers);
            }
        }

        public MockPgwProfile ConfigurePgwProfile(string code, Action<MockPgwProfile> configure)
        {
            lock (_gate)
            {
                if (!_pgwProfiles.TryGetValue(code, out var profile))
                    _pgwProfiles[code] = profile = new MockPgwProfile { Code = code, Priority = _pgwProfiles.Count + 1, CurrencyIds = _options.PgwCurrencyIds };

                configure(profile);
                return profile;
            }
        }

        public void SetCustomerOutcome(string paymentIntentId, MockCustomerOutcome outcome)
        {
            lock (_gate)
            {
                if (_operations.TryGetValue(paymentIntentId, out var operation)
                    && operation.Kind is MockOperationKind.PgwTransaction or MockOperationKind.BnplApplication)
                    operation.CustomerOutcome = outcome;
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

        public IReadOnlyList<MockCreditFacility> FacilitiesOf(PayerType payerType, long payerId, int currencyId)
        {
            lock (_gate)
                return _facilities.Values
                    .Where(facility => facility.PayerType == payerType && facility.PayerId == payerId && facility.CurrencyId == currencyId)
                    .Select(Copy)
                    .ToList();
        }

        public MockCreditFacility? FindCreditFacility(string facilityId)
        {
            lock (_gate)
                return _facilities.TryGetValue(facilityId, out var facility) ? Copy(facility) : null;
        }

        public bool IsCashAccepted(long? officeId, SalesChannel salesChannel)
        {
            lock (_gate)
                return officeId is { } office
                       && _cashOffices.Contains(office)
                       && (_cashChannels.Count == 0 || _cashChannels.Contains(salesChannel));
        }

        public bool IsBnplAvailable(PayerType payerType, long payerId, int currencyId)
        {
            lock (_gate)
                return BnplEnabled && !_bnplDisabledPayers.Contains((payerType, payerId)) && _options.BnplCurrencyIds.Contains(currencyId);
        }

        public IReadOnlyList<MockPgwProfile> PgwRoutes(int currencyId)
        {
            lock (_gate)
                return _pgwProfiles.Values
                    .Where(profile => profile.Enabled && profile.CurrencyIds.Contains(currencyId))
                    .OrderBy(profile => profile.Priority)
                    .ToList();
        }

        public MockPgwProfile? PgwProfile(string code)
        {
            lock (_gate)
                return _pgwProfiles.GetValueOrDefault(code);
        }

        public object Snapshot()
        {
            lock (_gate)
                return new
                {
                    wallets = _wallets.Values.Select(Copy).ToList(),
                    creditFacilities = _facilities.Values.Select(Copy).ToList(),
                    cash = new { officeIds = _cashOffices.ToList(), salesChannels = _cashChannels.ToList() },
                    bnpl = new { enabled = BnplEnabled, minimumAmount = BnplMinimumAmount, maximumAmount = BnplMaximumAmount, disabledPayers = _bnplDisabledPayers.ToList() },
                    pgwProfiles = _pgwProfiles.Values.OrderBy(profile => profile.Priority).ToList(),
                    operations = _operations.Values.ToList(),
                    routeAttempts = _routeAttempts.ToList()
                };
        }

        public MockOperation? FindOperation(string key)
        {
            lock (_gate)
                return _operations.GetValueOrDefault(key);
        }

        public IReadOnlyList<MockOperation> Operations
        {
            get
            {
                lock (_gate)
                    return _operations.Values.ToList();
            }
        }

        public IReadOnlyList<MockRouteAttempt> RouteAttempts
        {
            get
            {
                lock (_gate)
                    return _routeAttempts.ToList();
            }
        }

        public (MockOperation? Operation, string? FailureCode) DebitWallet(string key, string walletId, decimal amount, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (_operations.TryGetValue(key, out var existing))
                    return (existing, null);

                if (!_wallets.TryGetValue(walletId, out var wallet))
                    return (null, "FundingSourceUnavailable");

                if (wallet.Balance < amount)
                    return (null, "InsufficientFunds");

                wallet.Balance -= amount;
                return (Record(key, MockOperationKind.WalletDebit, walletId, amount, now), null);
            }
        }

        public (MockOperation? Operation, string? FailureCode) ReserveCredit(string key, string facilityId, decimal amount, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (_operations.TryGetValue(key, out var existing))
                    return (existing, null);

                if (!_facilities.TryGetValue(facilityId, out var facility))
                    return (null, "FundingSourceUnavailable");

                if (facility.Available < amount)
                    return (null, "InsufficientFunds");

                facility.Reserved += amount;
                return (Record(key, MockOperationKind.CreditReservation, facilityId, amount, now), null);
            }
        }

        public MockOperation RecordCashReceipt(string key, long officeId, decimal amount, DateTimeOffset now)
        {
            lock (_gate)
                return _operations.TryGetValue(key, out var existing)
                    ? existing
                    : Record(key, MockOperationKind.CashReceipt, $"office:{officeId}", amount, now, officeId: officeId);
        }

        public MockOperation OpenPgwTransaction(string key, string routeCode, decimal amount, DateTimeOffset now)
        {
            lock (_gate)
                return _operations.TryGetValue(key, out var existing)
                    ? existing
                    : Record(key, MockOperationKind.PgwTransaction, $"pgw:{routeCode}:{Guid.NewGuid():N}", amount, now, routeCode);
        }

        public MockOperation OpenBnplApplication(string key, decimal amount, DateTimeOffset now)
        {
            lock (_gate)
                return _operations.TryGetValue(key, out var existing)
                    ? existing
                    : Record(key, MockOperationKind.BnplApplication, $"bnpl:{Guid.NewGuid():N}", amount, now);
        }

        public void RecordRouteAttempt(string paymentIntentId, string routeCode, string result)
        {
            lock (_gate)
                _routeAttempts.Add(new MockRouteAttempt(paymentIntentId, routeCode, result));
        }

        public void MarkSettled(string key)
        {
            lock (_gate)
            {
                if (_operations.TryGetValue(key, out var operation))
                    operation.Settled = true;
            }
        }

        public void Release(string key)
        {
            lock (_gate)
            {
                if (!_operations.TryGetValue(key, out var operation) || operation.Released)
                    return;

                operation.Released = true;

                if (operation.Kind == MockOperationKind.CreditReservation && _facilities.TryGetValue(operation.Reference, out var facility))
                    facility.Reserved -= operation.Amount;
            }
        }

        private MockOperation Record(string key, MockOperationKind kind, string reference, decimal amount, DateTimeOffset now, string? routeCode = null, long? officeId = null)
            => _operations[key] = new MockOperation
            {
                Key = key,
                Kind = kind,
                Reference = reference,
                Amount = amount,
                CreatedAt = now,
                RouteCode = routeCode,
                OfficeId = officeId
            };

        private static MockWallet Copy(MockWallet wallet)
            => new() { Id = wallet.Id, Code = wallet.Code, PayerType = wallet.PayerType, PayerId = wallet.PayerId, CurrencyId = wallet.CurrencyId, Balance = wallet.Balance, IsDefault = wallet.IsDefault };

        private static MockCreditFacility Copy(MockCreditFacility facility)
            => new()
            {
                Id = facility.Id,
                TenderType = facility.TenderType,
                PayerType = facility.PayerType,
                PayerId = facility.PayerId,
                CurrencyId = facility.CurrencyId,
                Limit = facility.Limit,
                Reserved = facility.Reserved,
                AuthorizationValiditySeconds = facility.AuthorizationValiditySeconds
            };
    }
}
