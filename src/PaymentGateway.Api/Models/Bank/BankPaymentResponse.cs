using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Models.Bank;

public record BankPaymentResponse(
    [property: JsonPropertyName("authorized")] bool Authorized,
    [property: JsonPropertyName("authorization_code")] string? AuthorizationCode);
