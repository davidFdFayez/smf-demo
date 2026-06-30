namespace SMF.Domain.Enums;

public enum PaymentPurpose
{
    /// <summary>Annual membership fee. On success the Member transitions to Active.</summary>
    MembershipFee = 1,

    /// <summary>Entry fee for a tournament / event. Does not affect registration status.</summary>
    EventFee = 2,

    /// <summary>
    /// Federation e-commerce store purchase. On success the matching
    /// <see cref="SMF.Domain.Entities.Order"/> transitions to <c>Paid</c>
    /// and an <see cref="SMF.Domain.Entities.Invoice"/> is generated.
    /// </summary>
    ProductPurchase = 3
}
