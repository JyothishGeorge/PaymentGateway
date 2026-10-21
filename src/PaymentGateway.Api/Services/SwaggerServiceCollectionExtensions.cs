using Microsoft.OpenApi.Models;

using PaymentGateway.Api.Authentication;

namespace PaymentGateway.Api.Services;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // Adds an "Authorize" button to Swagger UI so the test API key can be supplied.
            options.AddSecurityDefinition(ApiKeyOptions.SchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-Api-Key",
                Description = "API key"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = ApiKeyOptions.SchemeName }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
