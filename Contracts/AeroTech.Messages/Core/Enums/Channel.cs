using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Core.Enums
{
    public enum Channel
    {
        [Display(Name = "BackOffice")] BackOffice = 1,

        [Display(Name = "IBE")] IBE = 2,

        [Display(Name = "Partner API")] PartnerAPI = 3,

        [Display(Name = "Agency Panel")] AgencyPanel = 4,

        [Display(Name = "GDS")] GDS = 5,


        [Display(Name = "Service")] Service = 98,
        
        [Display(Name = "System")] System = 99,

    }
}
