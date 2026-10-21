using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public static class MvcBuilderExtensions
{
    public static IMvcBuilder AddRejectedModelStateResponse(this IMvcBuilder mvcBuilder)
        => mvcBuilder.ConfigureApiBehaviorOptions(options =>
        {
            // Malformed requests (e.g. a non-integer amount) are reported as Rejected, consistent with validation failures.
            options.InvalidModelStateResponseFactory = static context => new BadRequestObjectResult(new RejectedPaymentResponse
            {
                Errors = context.ModelState
                    .SelectMany(static entry => entry.Value?.Errors.Select(error =>
                        string.IsNullOrEmpty(entry.Key) ? error.ErrorMessage : $"{entry.Key}: {error.ErrorMessage}") ?? [])
                    .ToList()
            });
        });
}
