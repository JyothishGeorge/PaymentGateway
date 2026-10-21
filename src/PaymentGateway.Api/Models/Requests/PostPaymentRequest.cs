namespace PaymentGateway.Api.Models.Requests;

// Modified some Fields to nullable so that "missing" can be distinguished from "invalid" during validation.
public class PostPaymentRequest
{
    public string? CardNumber { get; set; }
    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }
    public string? Currency { get; set; }
    public int? Amount { get; set; }
    public string? Cvv { get; set; }
}
