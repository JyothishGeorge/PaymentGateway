using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class AcquiringBankClientTests
{
    private static Task<Result<BankPaymentResponse>> SendAsync(HttpMessageHandler handler) =>
        new AcquiringBankClient(new HttpClient(handler) { BaseAddress = new Uri("http://bank.test/") }, NullLogger<AcquiringBankClient>.Instance)
            .ProcessPaymentAsync(TestData.BankRequest, CancellationToken.None);
    

    [Fact]
    public async Task SendsSnakeCaseRequestToPaymentsEndpoint()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, TestData.AuthorizedBankJson);

        // Act
        await SendAsync(handler);

        // Assert
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://bank.test/payments", handler.LastRequest.RequestUri!.ToString());
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            await handler.LastRequest.Content!.ReadAsStringAsync())!
            .ToDictionary(p => p.Key, p => p.Value.ToString());
        Assert.Equal(new Dictionary<string, string>
        {
            ["card_number"] = TestData.CardNumber,
            ["expiry_date"] = $"04/{TestData.NextYear}",
            ["currency"] = "GBP",
            ["amount"] = "1050",
            ["cvv"] = "123"
        }, body);
    }

    [Theory]
    [InlineData(TestData.AuthorizedBankJson, true, "abc")]
    [InlineData("""{"authorized":false,"authorization_code":""}""", false, "")]
    public async Task ParsesBankResponse(string json, bool authorized, string code)
    {
        // Act
        var result = await SendAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, json));

        // Assert
        Assert.Equal(new BankPaymentResponse(authorized, code), result.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(null)]   // network failure
    public async Task FailedCall_ReturnsBankUnavailable(HttpStatusCode? statusCode)
    {
        // Arrange
        var handler = statusCode is null
            ? StubHttpMessageHandler.Throwing(HttpRequestError.ConnectionError)
            : StubHttpMessageHandler.Returning(statusCode.Value);

        // Act
        var result = await SendAsync(handler);

        // Assert
        Assert.Equal(AcquiringBankClient.UnavailableError, result.Error);
    }
}
