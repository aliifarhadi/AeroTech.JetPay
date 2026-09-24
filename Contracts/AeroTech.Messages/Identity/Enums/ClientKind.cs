using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum ClientKind
    {
        [Display(Name = "BFF")] Bff = 1,
        [Display(Name = "Public Native")] PublicNative = 2,
        [Display(Name = "Confidential Machine")] ConfidentialMachine = 3
    }
}
