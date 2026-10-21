using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Queries;

public class GetPaymentQueryHandler
{
    private readonly IPaymentsRepository _repository;

    public GetPaymentQueryHandler(IPaymentsRepository repository)
    {
        _repository = repository;
    }

    public GetPaymentResponse? Handle(GetPaymentQuery query)
    {
        var payment = _repository.Get(query.Id);

        return payment is null ? null : GetPaymentResponse.From(payment);
    }
}
