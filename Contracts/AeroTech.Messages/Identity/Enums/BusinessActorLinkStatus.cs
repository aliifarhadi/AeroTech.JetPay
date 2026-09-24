using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum BusinessActorLinkStatus
    {
        [Display(Name = "Active")] Active = 1,
        [Display(Name = "Inactive")] Inactive = 2
    }
}
