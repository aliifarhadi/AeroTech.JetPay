namespace AeroTech.JetPay.Domain.Providers.Tenders
{
    /// <summary>
    /// Normalized payment-method eligibility. Provider codes and routes are resolved behind this port and never exposed.
    /// </summary>
    public interface IPaymentMethodOptionCatalog
    {
        Task<IReadOnlyList<PaymentMethodOption>> ResolveAsync(PaymentMethodEligibilityQuery query, CancellationToken cancellationToken = default);
    }
}
