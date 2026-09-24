using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum ClientPolicyStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Disabled")] Disabled = 2
    }
}
