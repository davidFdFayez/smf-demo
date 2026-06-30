namespace SMF.Domain.ValueObjects;

/// <summary>
/// Owned-type shipping address. Stored alongside the order so we always know
/// where the parcel went, even if the buyer later edits their member profile.
/// </summary>
public sealed class ShippingAddress
{
    public string RecipientName { get; private set; } = default!;
    public string Line1 { get; private set; } = default!;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = default!;
    public string Region { get; private set; } = default!;
    public string PostalCode { get; private set; } = default!;
    public string Country { get; private set; } = default!;
    public string PhoneNumber { get; private set; } = default!;

    private ShippingAddress() { }

    public static ShippingAddress Create(
        string recipientName,
        string line1,
        string? line2,
        string city,
        string region,
        string postalCode,
        string country,
        string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new ArgumentException("Recipient name is required.", nameof(recipientName));
        if (string.IsNullOrWhiteSpace(line1))
            throw new ArgumentException("Address line 1 is required.", nameof(line1));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.", nameof(city));
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));
        if (string.IsNullOrWhiteSpace(postalCode))
            throw new ArgumentException("Postal code is required.", nameof(postalCode));
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
            throw new ArgumentException("Country must be a 2-letter ISO code.", nameof(country));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        return new ShippingAddress
        {
            RecipientName = recipientName.Trim(),
            Line1 = line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            City = city.Trim(),
            Region = region.Trim(),
            PostalCode = postalCode.Trim(),
            Country = country.Trim().ToUpperInvariant(),
            PhoneNumber = phoneNumber.Trim()
        };
    }
}
