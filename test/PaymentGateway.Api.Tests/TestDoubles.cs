using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Moq;

using PaymentGateway.Api.Commands;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public static class TestAuth
{
    public const string HeaderName = "X-Api-Key";

    // Must match Authentication:ApiKey in the API's appsettings.json.
    public const string ApiKey = "pg_test_123e4567e89b12d3a456426614174000";
}

// The single valid payment that tests start from, in each of the shapes it takes through the gateway.
public static class TestData
{
    public const string CardNumber = "2222405343248877";
    public const string AuthorizedBankJson = """{"authorized":true,"authorization_code":"abc"}""";

    public static readonly int NextYear = DateTime.UtcNow.Year + 1;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = CardNumber,
        ExpiryMonth = 4,
        ExpiryYear = NextYear,
        Currency = "GBP",
        Amount = 1050,
        Cvv = "123"
    };

    // The valid request as JSON, with fields overridden by each `changes` JSON object in turn,
    // e.g. ValidJson("""{ "cvv": "12a" }"""). A null value sets the field to null (i.e. missing).
    public static JsonObject ValidJson(params string[] changes)
    {
        var payment = JsonSerializer.SerializeToNode(ValidRequest(), JsonOptions)!.AsObject();
        foreach (var (field, value) in changes.SelectMany(change => JsonNode.Parse(change)!.AsObject()))
        {
            payment[field] = value?.DeepClone();
        }
        return payment;
    }

    public static ProcessPaymentCommand Command => ProcessPaymentCommand.From(ValidRequest());

    public static BankPaymentRequest BankRequest => new(CardNumber, $"04/{NextYear}", "GBP", 1050, "123");

    public static Result<BankPaymentResponse> BankResponse(bool authorized) =>
        Result<BankPaymentResponse>.Success(new BankPaymentResponse(authorized, authorized ? "auth-code" : ""));

    public static Result<BankPaymentResponse> BankUnavailable =>
        Result<BankPaymentResponse>.Failure(AcquiringBankClient.UnavailableError);
}

// Moq helpers for the acquiring bank, shared by the handler and controller tests.
public static class BankMock
{
    private static readonly Expression<Func<IAcquiringBankClient, Task<Result<BankPaymentResponse>>>> AnyPayment =
        b => b.ProcessPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>());

    // A mock bank that authorizes every payment unless a test sets up a different response.
    public static Mock<IAcquiringBankClient> Create()
    {
        var bank = new Mock<IAcquiringBankClient>();
        bank.RespondsWith(TestData.BankResponse(true));
        return bank;
    }

    public static void RespondsWith(this Mock<IAcquiringBankClient> bank, Result<BankPaymentResponse> response) =>
        bank.Setup(AnyPayment).ReturnsAsync(response);

    public static void VerifyCalledOnceWith(this Mock<IAcquiringBankClient> bank, BankPaymentRequest request) =>
        bank.Verify(b => b.ProcessPaymentAsync(request, It.IsAny<CancellationToken>()), Times.Once);

    public static void VerifyNotCalled(this Mock<IAcquiringBankClient> bank) =>
        bank.Verify(AnyPayment, Times.Never);
}

// Stub for HttpMessageHandler.SendAsync.
public class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int Attempts { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Attempts++;
        LastRequest = request;
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Response(HttpStatusCode statusCode, string json = "{}") =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static StubHttpMessageHandler Returning(HttpStatusCode statusCode, string json = "{}") =>
        new(_ => Response(statusCode, json));

    public static StubHttpMessageHandler Throwing(HttpRequestError error) =>
        new(_ => throw new HttpRequestException(error));
}
