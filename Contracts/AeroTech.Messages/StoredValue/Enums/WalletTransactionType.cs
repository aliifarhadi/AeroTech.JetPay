using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum WalletTransactionType
    {
       [Display(Name = "Refund Credit Created")] RefundCreditCreated = 1,
       [Display(Name = "Goodwill Credit Created")] GoodwillCreditCreated = 2,
       [Display(Name = "Promotional Credit Created")] PromotionalCreditCreated = 3,
       [Display(Name = "Compensation Credit Created")] CompensationCreditCreated = 4,
       [Display(Name = "Agency Deposit Funded")] AgencyDepositFunded = 5,
       [Display(Name = "Authorization Placed")] AuthorizationPlaced = 6,
       [Display(Name = "Authorization Captured")] AuthorizationCaptured = 7,
       [Display(Name = "Authorization Released")] AuthorizationReleased = 8,
       [Display(Name = "Authorization Expired")] AuthorizationExpired = 9,
       [Display(Name = "Wallet Spend Restored")] WalletSpendRestored = 10,
       [Display(Name = "Fund Expired")] FundExpired = 11,
       [Display(Name = "Credit Reversed")] CreditReversed = 13,
       [Display(Name = "TopUp CreditCreated")] TopUpCreditCreated = 14,
       [Display(Name = "Administrative Credit Applied")] AdministrativeCreditApplied = 15,
       [Display(Name = "Administrative Debit Applied")] AdministrativeDebitApplied = 16,
       [Display(Name = "Value Blocked")] ValueBlocked = 17,
       [Display(Name = "Value Unblocked")] ValueUnblocked = 18,
       [Display(Name = "Deposit Claim Applied")] DepositClaimApplied = 19,
       [Display(Name = "Deposit Released")] DepositReleased = 20
    }
}
