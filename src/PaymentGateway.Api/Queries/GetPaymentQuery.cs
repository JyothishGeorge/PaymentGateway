namespace PaymentGateway.Api.Queries;

// Read side: fetches a previously processed payment.
public record GetPaymentQuery(Guid Id);
