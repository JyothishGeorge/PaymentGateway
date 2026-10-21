namespace PaymentGateway.Api.Models.Responses;

public class RejectedPaymentResponse
{
    public PaymentStatus Status { get; set; } = PaymentStatus.Rejected;
    public IReadOnlyList<string> Errors { get; set; } = [];
}
