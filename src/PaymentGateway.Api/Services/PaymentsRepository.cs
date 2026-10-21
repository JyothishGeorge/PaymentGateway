using System.Collections.Concurrent;

using PaymentGateway.Api.Models.Domain;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    void Add(Payment payment);
    Payment? Get(Guid id);
}

// In-memory test double standing in for a real storage engine.
public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public void Add(Payment payment)
    {
        _payments[payment.Id] = payment;
    }

    public Payment? Get(Guid id)
    {
        return _payments.GetValueOrDefault(id);
    }
}
