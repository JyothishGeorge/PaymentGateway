namespace PaymentGateway.Api.Authentication;

public static class ApiKeyAuthenticationExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(ApiKeyOptions.SchemeName)
            .AddScheme<ApiKeyOptions, ApiKeyAuthenticationHandler>(ApiKeyOptions.SchemeName, options =>
            {
                configuration.GetSection("Authentication").Bind(options);
                if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.MerchantId))
                {
                    throw new InvalidOperationException("Authentication:ApiKey and Authentication:MerchantId must be configured.");
                }
            });

        return services;
    }
}
