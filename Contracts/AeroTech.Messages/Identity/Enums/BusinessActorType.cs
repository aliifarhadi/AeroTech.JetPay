using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.Identity.Enums
{
    public enum BusinessActorType
    {
        [Display(Name = "Airline User")] AirlineUser = 1,
        [Display(Name = "Travel Agency User")] TravelAgencyUser = 2,
        [Display(Name = "Individual")] Individual = 3
    }
}
