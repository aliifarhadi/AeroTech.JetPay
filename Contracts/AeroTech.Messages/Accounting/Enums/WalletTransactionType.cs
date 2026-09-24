namespace AeroTech.Messages.Accounting.Enums
{
    public enum WalletTransactionType
    {
        TopUp = 1,              // Customer tops up
        ManualCredit = 2,       // Admin or system manually adds credit
        ManualDebit = 3,       // Admin or system manually deducts

        Payment = 10,           // Wallet used to pay for a booking or product
        Withdrawal = 11,        // Withdraw funds to bank account

        Hold = 20,              // Reserve funds for a pending booking
        ReleaseHold = 21,       // Release reserved funds 

        TopUpReversal = 30,     // Reverse a failed or cancelled top-up   
        PaymentReversal = 31,   // Refund a payment  

    }
}
