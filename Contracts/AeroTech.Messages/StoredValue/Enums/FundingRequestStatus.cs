using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.StoredValue.Enums
{
    public enum FundingRequestStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "Approved")] Approved = 2,
        [Display(Name = "Rejected")] Rejected = 3
    }
}
