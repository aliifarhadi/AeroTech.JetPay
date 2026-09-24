using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum WalletDebitReason
    {
        [Display(Name = "Order Payment")] OrderPayment = 1,
        [Display(Name = "Customer Withdrawal")] CustomerWithdrawal = 2,
        [Display(Name = "Deposit Claim")] DepositClaim = 3,
        [Display(Name = "Deposit Return")] DepositReturn = 4,
        [Display(Name = "Administrative Debit")] AdministrativeDebit = 5
    }
}
