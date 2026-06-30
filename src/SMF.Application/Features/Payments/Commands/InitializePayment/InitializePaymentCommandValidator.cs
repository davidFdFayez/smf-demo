using FluentValidation;

namespace SMF.Application.Features.Payments.Commands.InitializePayment;

public sealed class InitializePaymentCommandValidator : AbstractValidator<InitializePaymentCommand>
{
    public InitializePaymentCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEqual(Guid.Empty);
        RuleFor(x => x.AmountMinor).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.CallbackUrl).NotEmpty().Must(IsAbsoluteUri)
            .WithMessage("CallbackUrl must be an absolute URL the provider can POST to.");
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.Purpose).IsInEnum();
    }

    private static bool IsAbsoluteUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out _);
}
