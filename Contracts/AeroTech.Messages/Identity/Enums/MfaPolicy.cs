using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum MfaPolicy
    {
        [Display(Name = "Optional")] Optional = 1,
        [Display(Name = "Required")] Required = 2
    }
}
