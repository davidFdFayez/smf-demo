namespace SMF.Domain.Entities;

/// <summary>
/// Storefront category (e.g. "Gloves", "Shorts", "Pads"). Lightweight — just
/// a label + slug so the product list can be filtered without joining a heavy
/// taxonomy. Slug is unique and used in store URLs.
/// </summary>
public class ProductCategory
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ProductCategory() { }

    public static ProductCategory Create(
        string name,
        string slug,
        string? description,
        string? imageUrl,
        int displayOrder,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug is required.", nameof(slug));
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder));

        return new ProductCategory
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAtUtc = nowUtc
        };
    }

    public void UpdateDetails(string name, string? description, string? imageUrl, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.", nameof(name));
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        DisplayOrder = displayOrder;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
