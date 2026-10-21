namespace PaymentGateway.Api.Models.Domain;

public record Payment(
    Guid Id,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string? AuthorizationCode);
