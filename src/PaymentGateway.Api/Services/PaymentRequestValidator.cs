using FluentValidation;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public class PaymentRequestValidator : AbstractValidator<PostPaymentRequest>
{
    public static readonly IReadOnlySet<string> SupportedCurrencies = new HashSet<string> { "GBP", "USD", "EUR" };

    private readonly TimeProvider _timeProvider;

    public PaymentRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        RuleFor(x => x.CardNumber)
            .NotEmpty().WithMessage("Card number is required.");
        RuleFor(x => x.CardNumber)
            .Length(14, 19).WithMessage("Card number must be between 14 and 19 characters long.")
            .Must(BeNumeric).WithMessage("Card number must only contain numeric characters.")
            .When(x => !string.IsNullOrEmpty(x.CardNumber));

        RuleFor(x => x.ExpiryMonth)
            .NotNull().WithMessage("Expiry month is required.")
            .InclusiveBetween(1, 12).WithMessage("Expiry month must be between 1 and 12.");

        RuleFor(x => x.ExpiryYear)
            .NotNull().WithMessage("Expiry year is required.");
        RuleFor(x => x.ExpiryYear)
            .Must(year => year >= CurrentDate.Year).WithMessage("Expiry year must be in the future.")
            .When(x => x.ExpiryYear is not null);

        // A card is valid until the end of its expiry month, so the current month is still accepted.
        RuleFor(x => x)
            .Must(x => x.ExpiryMonth >= CurrentDate.Month)
            .WithName("ExpiryDate")
            .WithMessage("Card expiry date must be in the future.")
            .When(x => x.ExpiryMonth is >= 1 and <= 12 && x.ExpiryYear == CurrentDate.Year);

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.");
        RuleFor(x => x.Currency)
            .Length(3).WithMessage("Currency must be 3 characters long.")
            .When(x => !string.IsNullOrEmpty(x.Currency));
        RuleFor(x => x.Currency)
            .Must(currency => SupportedCurrencies.Contains(currency!))
            .WithMessage($"Currency must be one of: {string.Join(", ", SupportedCurrencies)}.")
            .When(x => x.Currency is { Length: 3 });

        RuleFor(x => x.Amount)
            .NotNull().WithMessage("Amount is required.")
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Cvv)
            .NotEmpty().WithMessage("CVV is required.");
        RuleFor(x => x.Cvv)
            .Length(3, 4).WithMessage("CVV must be 3 or 4 characters long.")
            .Must(BeNumeric).WithMessage("CVV must only contain numeric characters.")
            .When(x => !string.IsNullOrEmpty(x.Cvv));
    }

    private DateTimeOffset CurrentDate => _timeProvider.GetUtcNow();

    private static bool BeNumeric(string? value) => value is not null && value.All(char.IsAsciiDigit);
}
