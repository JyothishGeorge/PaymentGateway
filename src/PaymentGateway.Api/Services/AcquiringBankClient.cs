using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Services;

public interface IAcquiringBankClient
{
    Task<Result<BankPaymentResponse>> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken);
}

public class AcquiringBankClient : IAcquiringBankClient
{
    public const string UnavailableError = "The payment could not be processed by the acquiring bank. Please try again later.";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AcquiringBankClient> _logger;

    public AcquiringBankClient(HttpClient httpClient, ILogger<AcquiringBankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<BankPaymentResponse>> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("payments", request, cancellationToken);
        }
        // Any failure to get a response (network error, resilience timeout) means the
        // bank is unavailable, unless the caller itself cancelled the request.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Could not reach the acquiring bank");
            return Result<BankPaymentResponse>.Failure(UnavailableError);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Acquiring bank returned unexpected status code {StatusCode}", (int)response.StatusCode);
                return Result<BankPaymentResponse>.Failure(UnavailableError);
            }

            var bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken);
            if (bankResponse is null)
            {
                _logger.LogError("Acquiring bank returned an empty response");
                return Result<BankPaymentResponse>.Failure(UnavailableError);
            }

            return Result<BankPaymentResponse>.Success(bankResponse);
        }
    }
}
