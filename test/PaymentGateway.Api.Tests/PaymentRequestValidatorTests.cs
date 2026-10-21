using Moq;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

// Each case changes one field of a valid request.
public class PaymentRequestValidatorTests
{
    private readonly PaymentRequestValidator _validator =
        new(Mock.Of<TimeProvider>(clock => clock.GetUtcNow() == new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero)));

    private bool IsValid(Action<PostPaymentRequest> change)
    {
        var request = TestData.ValidRequest();
        change(request);
        return _validator.Validate(request).IsValid;
    }

    [Theory]
    [InlineData("12345678901234", true)]          // 14 digits
    [InlineData("1234567890123456789", true)]     // 19 digits
    [InlineData(null, false)]
    [InlineData("1234567890123", false)]          // 13 digits
    [InlineData("12345678901234567890", false)]   // 20 digits
    [InlineData("1234567890123a", false)]
    [InlineData("1234 5678 9012 34", false)]
    public void CardNumber(string? cardNumber, bool expected) =>
        Assert.Equal(expected, IsValid(r => r.CardNumber = cardNumber));

    // A card is valid until the end of its expiry month.
    [Theory]
    [InlineData(6, 2026, true)]                   // this month
    [InlineData(12, 2026, true)]
    [InlineData(1, 2027, true)]
    [InlineData(5, 2026, false)]                  // last month
    [InlineData(12, 2025, false)]                 // last year
    [InlineData(0, 2027, false)]
    [InlineData(13, 2027, false)]
    [InlineData(null, 2027, false)]
    [InlineData(4, null, false)]
    public void ExpiryDate(int? month, int? year, bool expected) =>
        Assert.Equal(expected, IsValid(r => (r.ExpiryMonth, r.ExpiryYear) = (month, year)));

    [Theory]
    [InlineData("GBP", true)]
    [InlineData("USD", true)]
    [InlineData("EUR", true)]
    [InlineData(null, false)]
    [InlineData("GB", false)]
    [InlineData("GBPP", false)]
    [InlineData("JPY", false)]                    // valid ISO code, but not supported
    [InlineData("gbp", false)]
    public void Currency(string? currency, bool expected) =>
        Assert.Equal(expected, IsValid(r => r.Currency = currency));

    [Theory]
    [InlineData(1, true)]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(-100, false)]
    public void Amount(int? amount, bool expected) =>
        Assert.Equal(expected, IsValid(r => r.Amount = amount));

    [Theory]
    [InlineData("123", true)]
    [InlineData("1234", true)]
    [InlineData("012", true)]                     // leading zero kept
    [InlineData(null, false)]
    [InlineData("12", false)]
    [InlineData("12345", false)]
    [InlineData("12a", false)]
    public void Cvv(string? cvv, bool expected) =>
        Assert.Equal(expected, IsValid(r => r.Cvv = cvv));

    [Fact]
    public void EmptyRequest_ReportsEveryMissingFieldAtOnce()
    {
        // Act
        var errors = _validator.Validate(new PostPaymentRequest()).Errors.Select(e => e.ErrorMessage);

        // Assert
        Assert.Equal(
            ["Card number is required.", "Expiry month is required.", "Expiry year is required.",
             "Currency is required.", "Amount is required.", "CVV is required."],
            errors);
    }
}
