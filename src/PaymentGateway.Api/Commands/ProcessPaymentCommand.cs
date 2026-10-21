using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Commands;

// Write side: asks the gateway to process a card payment.
public record ProcessPaymentCommand(
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string Cvv)
{
    // The request is expected to have already passed validation (see ValidationFilter).
    public static ProcessPaymentCommand From(PostPaymentRequest request) => new(
        request.CardNumber!,
        request.ExpiryMonth!.Value,
        request.ExpiryYear!.Value,
        request.Currency!,
        request.Amount!.Value,
        request.Cvv!);
}
