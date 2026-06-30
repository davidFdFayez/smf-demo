namespace SMF.Application.Features.Store.Cart.Common;

/// <summary>
/// Cart ownership at request time. Exactly one of <see cref="GuestKey"/> or
/// <see cref="MemberId"/> must be set; both being set means the call site
/// has already linked the guest to a member and we can merge.
/// </summary>
public sealed record CartIdentity(Guid? GuestKey, Guid? MemberId)
{
    public bool IsValid => GuestKey is { } g && g != Guid.Empty
                           || MemberId is { } m && m != Guid.Empty;
}
