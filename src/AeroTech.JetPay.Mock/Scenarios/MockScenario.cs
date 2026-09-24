namespace AeroTech.JetPay.Mock.Scenarios
{
    public enum MockScenario
    {
        IranianPgwCaptured = 1,
        IranianPgwFailed = 2,
        IranianPgwRequiresActionThenCaptured = 3,
        StoredValueCaptured = 4,
        StoredValueInsufficientBalance = 5,
        BnplRequiresActionThenAuthorized = 6,
        BnplRejected = 7,
        AgencyCreditAuthorized = 8,
        AgencyCreditInsufficientLimit = 9,
        PaymentProcessing = 10,
        PaymentExpired = 11,
        LateCapturedForSupersededInstruction = 12
    }
}
