using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum BusinessActorLinkSource
    {
        [Display(Name = "Registration")] Registration = 1,
        [Display(Name = "Invitation")] Invitation = 2,
        [Display(Name = "Migration")] Migration = 3,
        [Display(Name = "Admin Repair")] AdminRepair = 4
    }
}
