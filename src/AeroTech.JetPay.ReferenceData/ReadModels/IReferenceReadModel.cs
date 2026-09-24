namespace AeroTech.JetPay.ReferenceData.ReadModels
{
    public interface IReferenceReadModel<TKey>
    {
        TKey Id { get; }
        DateTimeOffset LastUpdateTime { get; }
    }
}
