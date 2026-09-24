using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum InvitationStatus
    {
        [Display(Name = "Pending")] Pending = 1,
        [Display(Name = "Accepted")] Accepted = 2,
        [Display(Name = "Expired")] Expired = 3,
        [Display(Name = "Revoked")] Revoked = 4
    }
}
