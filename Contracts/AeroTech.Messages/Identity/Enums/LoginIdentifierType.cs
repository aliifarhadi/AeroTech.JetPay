using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum LoginIdentifierType
    {
        [Display(Name = "Email")] Email = 1,
        [Display(Name = "User Name")] UserName = 2,
        [Display(Name = "Phone")] Phone = 3
    }
}
