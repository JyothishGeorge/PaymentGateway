using FluentValidation;

using PaymentGateway.Api.Commands;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Queries;

namespace PaymentGateway.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
        services.AddSingleton<IValidator<PostPaymentRequest>, PaymentRequestValidator>();
        services.AddScoped<ProcessPaymentCommandHandler>();
        services.AddScoped<GetPaymentQueryHandler>();

        return services;
    }
}
