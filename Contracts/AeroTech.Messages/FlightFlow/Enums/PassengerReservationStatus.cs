using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum PassengerReservationStatus
{
    [Display(Name = "Hold")] Hold = 1,
    
    [Display(Name = "Confirmed")] Confirmed, 

    [Display(Name = "Released")] Released, 
}