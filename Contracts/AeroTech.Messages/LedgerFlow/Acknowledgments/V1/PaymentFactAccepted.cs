namespace AeroTech.Messages.LedgerFlow.Acknowledgments.V1
{
    public sealed record PaymentFactAccepted(
        long PaymentAccountingFactSetId,
        string Destination,
        long JournalEntryId,
        bool ReferenceOnly) : BaseAcknowledgeCommand;
}
