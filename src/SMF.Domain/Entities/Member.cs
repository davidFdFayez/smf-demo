using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

public class Member
{
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = default!;
    public DateOnly DateOfBirth { get; private set; }
    public MemberRole Role { get; private set; }
    public string SMF_ID { get; private set; } = default!;
    public RegistrationStatus RegistrationStatus { get; private set; }
    public bool GuardianConsent { get; private set; }

    // Contact & identity (PDF §3 "Role-Specific Fields" + universally required
    // for the federation to reach the member about event schedules, payments,
    // and to cross-check national-ID against government athlete databases).
    public string Email { get; private set; } = default!;
    public string PhoneNumber { get; private set; } = default!;
    public string NationalId { get; private set; } = default!;

    // Compliance at signup (PDF §3 "Compliance at Signup" + §8 "Privacy &
    // Terms"). We store timestamps — not booleans — so the audit log can
    // answer "which version of the policy did this user accept, and when?"
    // rather than just "did they click the box".
    public DateTime TermsAcceptedAtUtc { get; private set; }
    public DateTime PrivacyPolicyAcceptedAtUtc { get; private set; }
    public DateTime CodeOfConductAcceptedAtUtc { get; private set; }

    // Role-specific profile fields (PDF §3). All optional at the domain level
    // so one Member row can cover any role; per-role validation happens in the
    // Application layer where we know the requested role.
    //
    //   AffiliatedClubId  — for Athlete/Coach/ClubAdmin; links to Clubs table.
    //   LicenseLevel      — for Coach/Referee (e.g. "IFMA-Level-2").
    //   YearsOfExperience — free-form integer the member self-reports; the
    //                       federation staff later validates & updates.
    public Guid? AffiliatedClubId { get; private set; }
    public string? LicenseLevel { get; private set; }
    public int? YearsOfExperience { get; private set; }

    // Athlete readiness (PDF §5 "Athlete Management"). Both fields are
    // optional at the Member level and only meaningful for athletes — the
    // application layer enforces the business rule ("athlete must have a
    // valid medical clearance before being added to a bracket").
    //
    //   WeightCategoryKg  — the weight class the athlete currently competes
    //                       at (bantamweight, featherweight, etc. encoded
    //                       numerically to avoid i18n drift).
    //   MedicalCleared    — latest medical-fitness check result. Null means
    //                       "never submitted"; false means "failed".
    //   MedicalClearedAtUtc — when the most recent medical was recorded.
    public decimal? WeightCategoryKg { get; private set; }
    public bool? MedicalCleared { get; private set; }
    public DateTime? MedicalClearedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Member() { }

    private Member(
        Guid id,
        string fullName,
        DateOnly dateOfBirth,
        MemberRole role,
        string smfId,
        bool guardianConsent,
        string email,
        string phoneNumber,
        string nationalId,
        DateTime acceptedAtUtc,
        Guid? affiliatedClubId,
        string? licenseLevel,
        int? yearsOfExperience)
    {
        Id = id;
        FullName = fullName;
        DateOfBirth = dateOfBirth;
        Role = role;
        SMF_ID = smfId;
        GuardianConsent = guardianConsent;
        Email = email;
        PhoneNumber = phoneNumber;
        NationalId = nationalId;
        TermsAcceptedAtUtc = acceptedAtUtc;
        PrivacyPolicyAcceptedAtUtc = acceptedAtUtc;
        CodeOfConductAcceptedAtUtc = acceptedAtUtc;
        AffiliatedClubId = affiliatedClubId;
        LicenseLevel = string.IsNullOrWhiteSpace(licenseLevel) ? null : licenseLevel.Trim();
        YearsOfExperience = yearsOfExperience;
        RegistrationStatus = RegistrationStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Member Register(
        string fullName,
        DateOnly dateOfBirth,
        MemberRole role,
        string smfId,
        bool guardianConsent,
        string email,
        string phoneNumber,
        string nationalId,
        DateTime acceptedAtUtc,
        Guid? affiliatedClubId = null,
        string? licenseLevel = null,
        int? yearsOfExperience = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        if (string.IsNullOrWhiteSpace(smfId))
            throw new ArgumentException("SMF_ID is required.", nameof(smfId));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        if (string.IsNullOrWhiteSpace(nationalId))
            throw new ArgumentException("National ID is required.", nameof(nationalId));

        if (acceptedAtUtc == default)
            throw new ArgumentException(
                "Compliance acceptance timestamp is required.", nameof(acceptedAtUtc));

        if (yearsOfExperience is < 0 or > 80)
            throw new ArgumentOutOfRangeException(
                nameof(yearsOfExperience),
                "Years of experience must be between 0 and 80.");

        return new Member(
            Guid.NewGuid(),
            fullName.Trim(),
            dateOfBirth,
            role,
            smfId,
            guardianConsent,
            email.Trim(),
            phoneNumber.Trim(),
            nationalId.Trim(),
            acceptedAtUtc,
            affiliatedClubId,
            licenseLevel,
            yearsOfExperience);
    }

    /// <summary>
    /// Bootstrap factory for seeding known identities (development seed data,
    /// integration test fixtures). Bypasses the normal ID generation so the
    /// caller can reference the member by a well-known <see cref="Guid"/>,
    /// and fills in placeholder contact + compliance fields so tests don't
    /// have to care about them.
    /// </summary>
    public static Member Seed(
        Guid id,
        string fullName,
        DateOnly dateOfBirth,
        MemberRole role,
        string smfId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Seed id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(smfId))
            throw new ArgumentException("SMF_ID is required.", nameof(smfId));

        var now = DateTime.UtcNow;
        var slug = smfId.Replace("#", string.Empty).Replace("-", string.Empty).ToLowerInvariant();

        var member = new Member(
            id,
            fullName.Trim(),
            dateOfBirth,
            role,
            smfId,
            guardianConsent: true,
            email: $"{slug}@seed.smf.local",
            phoneNumber: "+966500000000",
            nationalId: $"SEED-{slug}",
            acceptedAtUtc: now,
            affiliatedClubId: null,
            licenseLevel: null,
            yearsOfExperience: null)
        {
            RegistrationStatus = RegistrationStatus.Approved
        };
        return member;
    }

    public void Approve()
    {
        RegistrationStatus = RegistrationStatus.Approved;
    }

    /// <summary>
    /// Transitions the member into the fully active state after a successful
    /// membership-fee payment. Idempotent: calling it on an already-Active
    /// member is a no-op so duplicate webhook deliveries don't explode.
    /// </summary>
    public void ActivateAfterPayment()
    {
        if (RegistrationStatus == RegistrationStatus.Active) return;

        // A payment can only finalise a registration that an admin has
        // already approved. Activating a pending member would bypass the
        // manual review step — reject that explicitly rather than silently
        // upgrading the record.
        if (RegistrationStatus != RegistrationStatus.Approved)
            throw new InvalidOperationException(
                $"Member '{Id}' is in status {RegistrationStatus}; " +
                "only Approved members can be activated via payment.");

        RegistrationStatus = RegistrationStatus.Active;
    }

    /// <summary>
    /// Attaches the member to a specific club. Used when an athlete moves
    /// clubs or when an initially-independent registrant joins one later.
    /// </summary>
    public void AffiliateWithClub(Guid clubId)
    {
        if (clubId == Guid.Empty)
            throw new ArgumentException("Club id is required.", nameof(clubId));
        AffiliatedClubId = clubId;
    }

    public void DetachFromClub() => AffiliatedClubId = null;

    public int GetAgeOn(DateOnly onDate)
    {
        var age = onDate.Year - DateOfBirth.Year;
        if (DateOfBirth > onDate.AddYears(-age)) age--;
        return age;
    }

    public bool IsMinorOn(DateOnly onDate) => GetAgeOn(onDate) < 18;

    /// <summary>
    /// Flips <see cref="GuardianConsent"/> to <c>true</c> after a parental
    /// consent ceremony completes successfully. Called by the
    /// <c>ParentalConsent.Approve</c> command handler — the entity itself
    /// does not validate the approval, that's the workflow's job.
    /// </summary>
    public void RecordGuardianConsentApproval()
    {
        GuardianConsent = true;
    }

    /// <summary>
    /// Records the timestamps of the most recent policy acceptance ceremony.
    /// The audit trail of <i>which</i> versions were accepted lives in the
    /// <c>PolicyAcceptances</c> table — these timestamps are convenience
    /// columns for the legacy fast-path so existing queries keep working.
    /// </summary>
    public void RecordPolicyAcceptance(DateTime acceptedAtUtc, bool terms, bool privacy, bool codeOfConduct)
    {
        if (terms)         TermsAcceptedAtUtc         = acceptedAtUtc;
        if (privacy)       PrivacyPolicyAcceptedAtUtc = acceptedAtUtc;
        if (codeOfConduct) CodeOfConductAcceptedAtUtc = acceptedAtUtc;
    }

    /// <summary>
    /// Records the outcome of the most recent medical-clearance check.
    /// Used by §5 "Athlete Management" — athletes with
    /// <see cref="MedicalCleared"/> == false are ineligible for bracket entry.
    /// </summary>
    public void RecordMedicalClearance(bool cleared)
    {
        MedicalCleared = cleared;
        MedicalClearedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets or updates the athlete's weight-category (kilograms). Non-positive
    /// values are rejected so the business rule "every athlete in a bracket
    /// has a sane weight class" stays enforceable upstream.
    /// </summary>
    public void SetWeightCategory(decimal kg)
    {
        if (kg <= 0 || kg > 250)
            throw new ArgumentOutOfRangeException(nameof(kg),
                "Weight category must be between 0 and 250 kg.");
        WeightCategoryKg = kg;
    }
}
