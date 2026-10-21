namespace PaymentGateway.Api.Services;

public static class AcquiringBankServiceCollectionExtensions
{
    public static IHttpClientBuilder AddAcquiringBankClient(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>(client =>
            client.BaseAddress = new Uri(configuration["AcquiringBank:BaseUrl"]!));

        builder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);

            // Payment POSTs are not idempotent, so a retry after the bank may have received the request
            // (5xx, timeout, dropped response) could charge the shopper twice. Only retry when the
            // connection could not be established, i.e. the request never reached the bank.
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException
                {
                    HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError
                });
        });

        return builder;
    }
}
