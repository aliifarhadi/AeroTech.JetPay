namespace AeroTech.Messages.LedgerFlow.Acknowledgments.V1
{
    public sealed record WalletFactAccepted(
        long AccountingFactSetId,
        string Destination,
        long JournalEntryId) : BaseAcknowledgeCommand;
}
