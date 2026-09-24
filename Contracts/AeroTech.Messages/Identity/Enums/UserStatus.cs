using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum UserStatus
    {
        [Display(Name = "Pending Verification")] PendingVerification = 1,
        [Display(Name = "Provisioning")] Provisioning = 2,
        [Display(Name = "Active")] Active = 3,
        [Display(Name = "Disabled")] Disabled = 4,
        [Display(Name = "Closed")] Closed = 5
    }
}
