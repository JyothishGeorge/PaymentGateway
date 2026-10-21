using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Mvc.Testing;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests;

// End-to-end tests, run against the real bank simulator.
// Requires `docker-compose up`.
// The simulator decides by the card number's last digit: odd -> authorized, even -> declined, 0 -> 503.
[Trait("Category", "Integration")]
public class BankSimulatorIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AuthorizedCard = TestData.CardNumber;

    // The bank would answer 503 for this card, so a Rejected result proves the bank was not called.
    private const string BankErrorCard = "2222405343248870";

    private static readonly JsonSerializerOptions JsonOptions = TestData.JsonOptions;

    private static readonly DateTime Today = DateTime.UtcNow;

    private readonly HttpClient _client;

    public BankSimulatorIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add(TestAuth.HeaderName, TestAuth.ApiKey);
    }

    // Expiry combinations depend on today's date, so they cannot be [InlineData].
    public static TheoryData<string> ValidExpiries => new()
    {
        Expiry(Today.Month, Today.Year + 1)
    };

    public static TheoryData<string> InvalidExpiries => new()
    {
        Expiry(Today.AddMonths(-1).Month, Today.AddMonths(-1).Year)
    };

    [Theory]
    [InlineData("2222405343248871", PaymentStatus.Authorized)]
    [InlineData("2222405343248873", PaymentStatus.Authorized)]
    [InlineData("2222405343248875", PaymentStatus.Authorized)]
    [InlineData("2222405343248877", PaymentStatus.Authorized)]
    [InlineData("2222405343248879", PaymentStatus.Authorized)]
    [InlineData("2222405343248872", PaymentStatus.Declined)]
    [InlineData("2222405343248874", PaymentStatus.Declined)]
    [InlineData("2222405343248876", PaymentStatus.Declined)]
    [InlineData("2222405343248878", PaymentStatus.Declined)]
    public async Task Payment_HasStatusDecidedByBank(string cardNumber, PaymentStatus expectedStatus)
    {
        // Act
        var (payment, _) = await PostAsync(Payment(cardNumber));

        // Assert
        Assert.Equal(expectedStatus, payment.Status);
    }

    [Theory]
    [InlineData("2222405343248877", PaymentStatus.Authorized)]
    [InlineData("2222405343248878", PaymentStatus.Declined)]
    public async Task ProcessedPayment_CanBeRetrievedById_WithMaskedCardNumber(string cardNumber, PaymentStatus expectedStatus)
    {
        // Arrange
        var (created, postBody) = await PostAsync(Payment(cardNumber));

        // Act
        var getBody = await _client.GetStringAsync($"/api/payments/{created.Id}");
        var retrieved = JsonSerializer.Deserialize<GetPaymentResponse>(getBody, JsonOptions);

        // Assert
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(expectedStatus, created.Status);
        Assert.Equal(cardNumber[^4..], created.CardNumberLastFour);
        Assert.Equal((4, Today.Year + 1, "GBP", 1050), (created.ExpiryMonth, created.ExpiryYear, created.Currency, created.Amount));
        Assert.Equivalent(created, retrieved);
        Assert.DoesNotContain(cardNumber, postBody);
        Assert.DoesNotContain(cardNumber, getBody);
    }

    [Theory]
    [InlineData("""{ "cardNumber": "22224053432481" }""")]        // 14 characters
    [InlineData("""{ "cardNumber": "2222405343248812341" }""")]   // 19 characters
    [InlineData("""{ "expiryMonth": 1 }""")]
    [InlineData("""{ "expiryMonth": 12 }""")]
    [InlineData("""{ "amount": 1 }""")]                          // $0.01 is supplied as 1
    [InlineData("""{ "cvv": "1234" }""")]                        // 4 characters
    [MemberData(nameof(ValidExpiries))]
    public async Task ValidPayment_IsSentToBank(string changes)
    {
        // Act
        var (payment, _) = await PostAsync(Payment(AuthorizedCard, changes));

        // Assert
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Theory]
    [InlineData("""{ "cardNumber": null }""")]
    [InlineData("""{ "cardNumber": "2222405343240" }""")]         // 13 characters
    [InlineData("""{ "cardNumber": "22224053432488771230" }""")]  // 20 characters
    [InlineData("""{ "cardNumber": "2222 4053 4324 8870" }""")]   // non-numeric
    [InlineData("""{ "expiryMonth": null }""")]
    [InlineData("""{ "expiryMonth": 0 }""")]
    [InlineData("""{ "expiryMonth": 13 }""")]
    [InlineData("""{ "expiryYear": null }""")]
    [InlineData("""{ "expiryYear": 2000 }""")]
    [InlineData("""{ "currency": null }""")]
    [InlineData("""{ "currency": "GB" }""")]
    [InlineData("""{ "currency": "GBPP" }""")]
    [InlineData("""{ "amount": null }""")]
    [InlineData("""{ "amount": 10.50 }""")]                       // not an integer
    [InlineData("""{ "cvv": null }""")]
    [InlineData("""{ "cvv": "12" }""")]
    [InlineData("""{ "cvv": "12345" }""")]
    [InlineData("""{ "cvv": "12a" }""")]
    [MemberData(nameof(InvalidExpiries))]
    public async Task InvalidPayment_IsRejectedWithoutCallingBank(string changes)
    {
        // Act
        var (payment, _) = await PostAsync(Payment(BankErrorCard, changes));

        // Assert
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal(Guid.Empty, payment.Id);
    }

    [Fact]
    public async Task NoMoreThanThreeCurrencies_AreAccepted()
    {
        // Arrange
        string[] isoCurrencies = ["GBP", "USD", "EUR", "JPY", "CHF", "AUD", "CAD", "CNY", "SEK", "NZD", "INR", "SGD"];

        // Act
        var accepted = new List<string>();
        foreach (var currency in isoCurrencies)
        {
            var (payment, _) = await PostAsync(Payment(AuthorizedCard, $$"""{ "currency": "{{currency}}" }"""));
            if (payment.Status != PaymentStatus.Rejected)
            {
                accepted.Add(currency);
            }
        }

        // Assert
        Assert.Contains("GBP", accepted);
        Assert.InRange(accepted.Count, 1, 3);
    }

    private static string Expiry(int month, int year) => $$"""{ "expiryMonth": {{month}}, "expiryYear": {{year}} }""";

    // A valid payment for the given card, with any fields in changes overriding the defaults.
    private static JsonObject Payment(string cardNumber, string changes = "{}") =>
        TestData.ValidJson($$"""{ "cardNumber": "{{cardNumber}}" }""", changes);

    // Authorized, Declined and Rejected responses all carry a status.
    private async Task<(PostPaymentResponse Payment, string Body)> PostAsync(JsonObject payment)
    {
        var response = await _client.PostAsync("/api/payments",
            new StringContent(payment.ToJsonString(), Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        return (JsonSerializer.Deserialize<PostPaymentResponse>(body, JsonOptions)!, body);
    }
}
