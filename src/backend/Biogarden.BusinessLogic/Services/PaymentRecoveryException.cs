namespace Biogarden.BusinessLogic.Services;

public sealed class PaymentRecoveryException : Exception
{
    public PaymentRecoveryException() : base("A fizetés indításának eredményét még ellenőrizzük. Ne hozz létre új rendelést; próbáld újra később, vagy a rendelési számmal kérj segítséget.") { }
}
