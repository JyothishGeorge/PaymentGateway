using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Domain;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Commands;

public class ProcessPaymentCommandHandler
{
    private readonly IAcquiringBankClient _bankClient;
    private readonly IPaymentsRepository _repository;

    public ProcessPaymentCommandHandler(IAcquiringBankClient bankClient, IPaymentsRepository repository )
    {
        _bankClient = bankClient;
        _repository = repository;
    }

    public async Task<Result<Payment>> HandleAsync(ProcessPaymentCommand command, CancellationToken cancellationToken)
    {
        var bankResult = await _bankClient.ProcessPaymentAsync(
            new BankPaymentRequest(
                command.CardNumber,
                $"{command.ExpiryMonth:D2}/{command.ExpiryYear}",
                command.Currency,
                command.Amount,
                command.Cvv),
            cancellationToken);

        if (!bankResult.IsSuccess)
        {
            return Result<Payment>.Failure(bankResult.Error!);
        }

        var bankResponse = bankResult.Value!;
        var payment = new Payment(
            Guid.NewGuid(),
            bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            command.CardNumber[^4..],
            command.ExpiryMonth,
            command.ExpiryYear,
            command.Currency,
            command.Amount,
            bankResponse.AuthorizationCode);

        _repository.Add(payment);

        return Result<Payment>.Success(payment);
    }
}
