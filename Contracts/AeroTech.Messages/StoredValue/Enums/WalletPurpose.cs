using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum WalletPurpose
    {
        [Display(Name = "General Cash")] GeneralCash = 1,
        [Display(Name = "Business Prepaid")] BusinessPrepaid = 2,
        [Display(Name = "Charter Advance")] CharterAdvance = 3,
        [Display(Name = "Security Deposit")] SecurityDeposit = 4
    }
}
