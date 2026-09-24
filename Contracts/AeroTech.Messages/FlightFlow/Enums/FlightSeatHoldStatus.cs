using System.ComponentModel.DataAnnotations;

namespace AeroTech.Messages.FlightFlow.Enums;

public enum FlightSeatHoldStatus
{
    [Display(Name = "Held")] Held = 1,
    [Display(Name = "Released")] Released,
    [Display(Name = "Expired")] Expired,
    [Display(Name = "Confirmed")] Confirmed,
    [Display(Name = "Canceled")] Cancelled
}