using Microsoft.AspNetCore.Authentication;

namespace PaymentGateway.Api.Authentication;

public class ApiKeyOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";

    public string HeaderName { get; set; } = "X-Api-Key";

    // The single key accepted by the gateway (test-only; a real system would issue per-merchant keys).
    public string ApiKey { get; set; } = string.Empty;

    // The merchant identity assigned to requests authenticated with ApiKey.
    public string MerchantId { get; set; } = string.Empty;
}
