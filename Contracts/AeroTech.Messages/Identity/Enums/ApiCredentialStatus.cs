using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum ApiCredentialStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Revoked")] Revoked = 2,
        [Display(Name = "Expired")] Expired = 3
    }
}
