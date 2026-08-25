using FluentValidation;
using KlyonHubPlatform.Application.DTOs.Payments;
using KlyonHubPlatform.Application.Helpers;

namespace KlyonHubPlatform.Application.Validators
{
    public class CreditCardHolderBillingInputValidator : AbstractValidator<CreditCardHolderBillingInput>
    {
        public CreditCardHolderBillingInputValidator()
        {
            RuleFor(x => x).Custom((input, context) =>
            {
                if (!CreditCardHolderBilling.TryNormalize(
                        input.PostalCode,
                        input.AddressNumber,
                        input.AddressHasNoNumber,
                        out _,
                        out _,
                        out var error))
                {
                    var property = error.Contains("CEP", StringComparison.OrdinalIgnoreCase)
                        ? nameof(input.PostalCode)
                        : nameof(input.AddressNumber);
                    context.AddFailure(property, error);
                }
            });
        }
    }
}
