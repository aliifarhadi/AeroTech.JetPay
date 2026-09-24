using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum AuthenticationFlowStatus
    {
        [Display(Name = "Pending MFA")] PendingMfa = 1,
        [Display(Name = "Ready For Context")] ReadyForContext = 2,
        [Display(Name = "Completed")] Completed = 3,
        [Display(Name = "Expired")] Expired = 4,
        [Display(Name = "Failed")] Failed = 5
    }
}
