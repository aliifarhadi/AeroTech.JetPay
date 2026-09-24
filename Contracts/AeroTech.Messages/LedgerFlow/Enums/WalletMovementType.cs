using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.LedgerFlow.Enums
{
    // The ledger's interpretation of a StoredValue FactType. Funding-side types raise the stored-value
    // liability; redemption-side types lower it.
    public enum WalletMovementType
    {
        [Display(Name = "Funding")] Funding = 1,
        [Display(Name = "Redemption")] Redemption = 2,
        [Display(Name = "Redemption Reversal")] RedemptionReversal = 3,
        [Display(Name = "Funding Reversal")] FundingReversal = 4,
        [Display(Name = "Receivable Conversion")] ReceivableConversion = 5,
        // A fact type the interpreter does not recognise: recorded for traceability, posts nothing, moves nothing.
        [Display(Name = "Unclassified")] Unclassified = 6,
        [Display(Name = "Adjustment Credit")] AdjustmentCredit = 7,
        [Display(Name = "Adjustment Debit")] AdjustmentDebit = 8,

        // Deposit/advance funding and return move a separate liability, not the stored-value liability, so their
        // stored-value delta is zero (they must not disturb the wallet three-way reconstruction).
        [Display(Name = "Deposit Funding")] DepositFunding = 9,
        [Display(Name = "Deposit Return")] DepositReturn = 10,
        // A customer withdrawing stored value lowers the stored-value liability.
        [Display(Name = "Customer Withdrawal")] CustomerWithdrawal = 11
    }
}
