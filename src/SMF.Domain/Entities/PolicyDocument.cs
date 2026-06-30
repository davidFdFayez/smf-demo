using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// A single immutable version of a Terms / Privacy / Code-of-Conduct
/// document. Once published, the body is never edited — instead a new
/// version row is created. <see cref="PolicyAcceptance"/> rows reference
/// the exact <c>PolicyDocument.Id</c>, giving every audit trail an
/// answer to "exactly which words did the user agree to?".
/// </summary>
public class PolicyDocument
{
    public Guid Id { get; private set; }
    public PolicyDocumentKind Kind { get; private set; }

    /// <summary>Semantic version, e.g. "2025-09" or "v3". Free-form so
    /// editorial teams can pick whatever scheme fits their workflow.</summary>
    public string Version { get; private set; } = default!;

    public string Title { get; private set; } = default!;

    /// <summary>Markdown body of the policy. Rendered client-side.</summary>
    public string BodyMarkdown { get; private set; } = default!;

    /// <summary>SHA-256 of the body — printed on legal letterheads if a
    /// dispute escalates and a regulator wants to verify the version
    /// served at signup matches what's in the audit log.</summary>
    public string ContentHash { get; private set; } = default!;

    public bool IsActive { get; private set; }
    public DateTime EffectiveAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? PublishedByMemberId { get; private set; }

    private PolicyDocument() { }

    public static PolicyDocument Publish(
        PolicyDocumentKind kind,
        string version,
        string title,
        string bodyMarkdown,
        DateTime effectiveAtUtc,
        Guid? publishedByMemberId)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version is required.", nameof(version));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(bodyMarkdown))
            throw new ArgumentException("Body is required.", nameof(bodyMarkdown));
        if (effectiveAtUtc == default)
            throw new ArgumentException("Effective-at timestamp is required.", nameof(effectiveAtUtc));

        return new PolicyDocument
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Version = version.Trim(),
            Title = title.Trim(),
            BodyMarkdown = bodyMarkdown,
            ContentHash = ComputeHash(bodyMarkdown),
            IsActive = true,
            EffectiveAtUtc = effectiveAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            PublishedByMemberId = publishedByMemberId
        };
    }

    public void Retire() => IsActive = false;

    /// <summary>SHA-256 hex of the policy body. Pulled out so the
    /// repository / handler can recompute it for an existing version
    /// without instantiating an aggregate.</summary>
    public static string ComputeHash(string body)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
