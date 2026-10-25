using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Domain;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

// In-memory API tests: the real HTTP pipeline with a mocked acquiring bank and the in-memory repository.
public class PaymentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string PaymentsPath = "/api/payments";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IAcquiringBankClient> _bank = BankMock.Create();
    private readonly PaymentsRepository _repository = new();

    public PaymentsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(string? apiKey = TestAuth.ApiKey)
    {
        var client = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcquiringBankClient>();
            services.AddSingleton(_bank.Object);
            services.RemoveAll<IPaymentsRepository>();
            services.AddSingleton<IPaymentsRepository>(_repository);
        })).CreateClient();

        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuth.HeaderName, apiKey);
        }
        return client;
    }

    // Posts the valid payment, with any fields in `changes` overriding it.
    private Task<HttpResponseMessage> PostAsync(string changes = "{}") =>
        CreateClient().PostAsJsonAsync(PaymentsPath, TestData.ValidJson(changes));

    private Task<HttpResponseMessage> GetAsync(Guid id) => CreateClient().GetAsync($"{PaymentsPath}/{id}");

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(TestData.JsonOptions))!;

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task PostPayment_ProcessedByBank_ReturnsCreatedMaskedPayment(bool authorized, PaymentStatus expectedStatus)
    {
        // Arrange
        _bank.RespondsWith(TestData.BankResponse(authorized));

        // Act
        var response = await PostAsync();
        var body = await response.Content.ReadAsStringAsync();
        var payment = await ReadAsync<PostPaymentResponse>(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/Payments/{payment.Id}", response.Headers.Location!.AbsolutePath);
        _bank.VerifyCalledOnceWith(TestData.BankRequest);
        Assert.Equivalent(new
        {
            Status = expectedStatus,
            CardNumberLastFour = "8877",
            ExpiryMonth = 4,
            ExpiryYear = TestData.NextYear,
            Currency = "GBP",
            Amount = 1050
        }, payment);
        Assert.Contains($"\"status\":\"{expectedStatus}\"", body);
        Assert.DoesNotContain(TestData.CardNumber, body);
        Assert.DoesNotContain("cvv", body);
    }

    [Theory]
    [InlineData("""{ "cardNumber": "123", "expiryMonth": 13, "currency": "JPY", "amount": 0, "cvv": "1" }""")]
    [InlineData("""{ "amount": 10.5 }""")]   // malformed: not an integer
    [InlineData("""{ "cvv": "12a" }""")]
    public async Task PostPayment_InvalidRequest_IsRejectedWithoutCallingBank(string changes)
    {
        // Act
        var response = await PostAsync(changes);
        var rejected = await ReadAsync<RejectedPaymentResponse>(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Rejected, rejected.Status);
        Assert.NotEmpty(rejected.Errors);
        _bank.VerifyNotCalled();
    }

    [Fact]
    public async Task PostPayment_BankUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        _bank.RespondsWith(TestData.BankUnavailable);

        // Act
        var response = await PostAsync();

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task GetPayment_ReturnsStoredPayment()
    {
        // Arrange
        var payment = new Payment(Guid.NewGuid(), PaymentStatus.Declined, "0123", 12, 2030, "USD", 1, null);
        _repository.Add(payment);

        // Act
        var response = await GetAsync(payment.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equivalent(GetPaymentResponse.From(payment), await ReadAsync<GetPaymentResponse>(response));
    }

    [Fact]
    public async Task GetPayment_UnknownId_ReturnsNotFound()
    {
        // Act
        var response = await GetAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The POST has no body, so a 401 (rather than a 400) also shows authentication runs before validation.
    [Theory]
    [InlineData("POST", PaymentsPath, null)]
    [InlineData("POST", PaymentsPath, "wrong-key")]
    [InlineData("GET", $"{PaymentsPath}/00000000-0000-0000-0000-000000000001", null)]
    public async Task Request_WithoutValidApiKey_IsUnauthorized(string method, string url, string? apiKey)
    {
        // Act
        var response = await CreateClient(apiKey).SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _bank.VerifyNotCalled();
    }

    [Fact]
    public async Task Health_WithoutApiKey_ReturnsHealthy()
    {
        // Act
        var response = await CreateClient(apiKey: null).GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
