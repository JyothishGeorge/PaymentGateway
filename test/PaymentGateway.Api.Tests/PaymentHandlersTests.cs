using Moq;

using PaymentGateway.Api.Commands;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Domain;
using PaymentGateway.Api.Queries;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentHandlersTests
{
    private readonly Mock<IAcquiringBankClient> _bank = BankMock.Create();
    private readonly Mock<IPaymentsRepository> _repository = new();

    private Task<Result<Payment>> ProcessAsync() =>
        new ProcessPaymentCommandHandler(_bank.Object, _repository.Object).HandleAsync(TestData.Command, CancellationToken.None);

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ProcessPayment_SendsBankRequestAndStoresMaskedPayment(bool authorized, PaymentStatus expectedStatus)
    {
        // Arrange
        _bank.RespondsWith(TestData.BankResponse(authorized));

        // Act
        var result = await ProcessAsync();

        // Assert
        var payment = result.Value!;
        _bank.VerifyCalledOnceWith(TestData.BankRequest);
        Assert.Equal(expectedStatus, payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        _repository.Verify(r => r.Add(payment), Times.Once);
    }

    [Fact]
    public async Task ProcessPayment_BankUnavailable_ReturnsFailureAndStoresNothing()
    {
        // Arrange
        _bank.RespondsWith(TestData.BankUnavailable);

        // Act
        var result = await ProcessAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(AcquiringBankClient.UnavailableError, result.Error);
        _repository.Verify(r => r.Add(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public void GetPayment_ReturnsStoredPaymentOrNull()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), PaymentStatus.Authorized, "8877", 4, 2030, "GBP", 1050, "auth-code");
        _repository.Setup(r => r.Get(payment.Id)).Returns(payment);
        var handler = new GetPaymentQueryHandler(_repository.Object);

        // Act
        var existing = handler.Handle(new GetPaymentQuery(payment.Id));
        var unknown = handler.Handle(new GetPaymentQuery(Guid.NewGuid()));

        // Assert
        Assert.Equal(payment.Id, existing!.Id);
        Assert.Equal("8877", existing.CardNumberLastFour);
        Assert.Null(unknown);
    }
}
