namespace AeroTech.Messages.LedgerFlow.Acknowledgments.V1
{
    public sealed record WalletFactRejected(
        long AccountingFactSetId,
        string Destination,
        string RejectionCode) : BaseAcknowledgeCommand;
}
