namespace AeroTech.Messages.LedgerFlow.Acknowledgments.V1
{
    public sealed record PaymentFactRejected(
        long PaymentAccountingFactSetId,
        string Destination,
        string RejectionCode) : BaseAcknowledgeCommand;
}
