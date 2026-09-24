using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum SecuritySessionStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Revoked")] Revoked = 2
    }
}
