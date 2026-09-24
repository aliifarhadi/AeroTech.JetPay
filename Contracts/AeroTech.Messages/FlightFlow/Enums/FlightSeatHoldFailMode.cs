namespace AeroTech.Messages.FlightFlow.Enums
{
    public enum FlightSeatHoldFailMode
    {
        AllOrNothing=1,
        PartialAllowed
    }

    public enum FlightSeatHoldCancellationReason
    {
        PaymentFailed = 1,
        PaxRequest
    }
}
