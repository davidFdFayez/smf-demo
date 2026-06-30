using System.Text.RegularExpressions;
using FluentValidation;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Members.Commands.RegisterMember;

public sealed class RegisterMemberCommandValidator : AbstractValidator<RegisterMemberCommand>
{
    // Loose phone format — accepts +countrycode and spaces/dashes. Kept
    // intentionally permissive so international referees can register
    // without us getting in a fight with every national numbering plan.
    private static readonly Regex PhonePattern =
        new(@"^\+?[0-9][0-9\s\-]{6,20}$", RegexOptions.Compiled);

    // Saudi national IDs / Iqamas are 10 digits. We accept any 5-30 char
    // alphanumeric so the same field can hold foreign passport numbers
    // for international athletes. Tighten per-role in a later slice.
    private static readonly Regex NationalIdPattern =
        new(@"^[A-Za-z0-9\-]{5,30}$", RegexOptions.Compiled);

    public RegisterMemberCommandValidator(IDateTimeProvider dateTime)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200);

        RuleFor(x => x.DateOfBirth)
            .NotEqual(default(DateOnly)).WithMessage("Date of birth is required.")
            .Must(dob => dob <= dateTime.Today)
                .WithMessage("Date of birth must be in the past.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Role is invalid.");

        // Business rule (PDF §3 + Compliance & Governance module): minor
        // athletes need parental consent. Two acceptance paths:
        //   (a) Legacy fast-path — the parent signed in person, the staff
        //       ticks <c>GuardianConsent</c> on the registration form.
        //   (b) Self-service — the registrant supplies guardian details
        //       and a digital consent ceremony is issued automatically.
        // Either path is acceptable; the validator reports a single clear
        // <c>GuardianConsent</c> error if neither path is satisfied.
        RuleFor(x => x.GuardianConsent)
            .Must((cmd, _) => cmd.GuardianConsent || HasGuardianDetails(cmd))
            .When(x => x.Role == MemberRole.Athlete && IsMinor(x.DateOfBirth, dateTime.Today))
            .WithMessage(
                "Guardian consent is required for athletes under 18. " +
                "Either set GuardianConsent=true (in-person signature) " +
                "or supply GuardianFullName, GuardianEmail, GuardianPhone, and GuardianRelation.");

        RuleFor(x => x.GuardianEmail)
            .EmailAddress().MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.GuardianEmail))
            .WithMessage("Guardian email must be a valid email address.");

        RuleFor(x => x.GuardianPhone)
            .MaximumLength(32)
            .When(x => !string.IsNullOrWhiteSpace(x.GuardianPhone));

        RuleFor(x => x.GuardianFullName)
            .MaximumLength(200);

        RuleFor(x => x.GuardianRelation)
            .IsInEnum().When(x => x.GuardianRelation.HasValue);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(200)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(32)
            .Matches(PhonePattern)
                .WithMessage("Phone number format is invalid.");

        RuleFor(x => x.NationalId)
            .NotEmpty().WithMessage("National ID is required.")
            .MaximumLength(32)
            .Matches(NationalIdPattern)
                .WithMessage("National ID must be 5–30 letters, digits, or dashes.");

        // Compliance at signup (PDF §3 + §8). Three distinct policies so we
        // can evolve them independently and report per-policy consent rates
        // for SOPC governance audits.
        RuleFor(x => x.AcceptTerms)
            .Equal(true).WithMessage("You must accept the Terms of Service.");

        RuleFor(x => x.AcceptPrivacyPolicy)
            .Equal(true).WithMessage("You must accept the Privacy Policy.");

        RuleFor(x => x.AcceptCodeOfConduct)
            .Equal(true).WithMessage("You must accept the Code of Conduct.");

        // Role-specific profile fields (PDF §3 "Role-Specific Registration").
        // Coaches and referees must declare a license level so the federation
        // can route them to appropriate matches/seminars. Club admins must
        // nominate the club they administer.
        RuleFor(x => x.LicenseLevel)
            .NotEmpty().WithMessage("License level is required for coaches.")
            .MaximumLength(64)
            .When(x => x.Role == MemberRole.Coach);

        RuleFor(x => x.LicenseLevel)
            .NotEmpty().WithMessage("License level is required for referees.")
            .MaximumLength(64)
            .When(x => x.Role == MemberRole.Referee);

        RuleFor(x => x.YearsOfExperience)
            .InclusiveBetween(0, 80)
            .When(x => x.YearsOfExperience.HasValue)
            .WithMessage("Years of experience must be between 0 and 80.");

        RuleFor(x => x.AffiliatedClubId)
            .NotEqual(Guid.Empty).WithMessage("Affiliated club is required for club admins.")
            .When(x => x.Role == MemberRole.ClubAdmin);
    }

    private static bool IsMinor(DateOnly dateOfBirth, DateOnly today)
    {
        if (dateOfBirth == default) return false;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age < 18;
    }

    private static bool HasGuardianDetails(RegisterMemberCommand cmd)
        => !string.IsNullOrWhiteSpace(cmd.GuardianFullName)
        && !string.IsNullOrWhiteSpace(cmd.GuardianEmail)
        && !string.IsNullOrWhiteSpace(cmd.GuardianPhone)
        && cmd.GuardianRelation.HasValue;
}
