using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum ExternalLoginStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Revoked")] Revoked = 2
    }
}
