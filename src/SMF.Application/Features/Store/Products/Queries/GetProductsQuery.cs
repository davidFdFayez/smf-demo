using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Members.Queries.GetMembers;
using SMF.Application.Features.Store.Common;

namespace SMF.Application.Features.Store.Products.Queries;

/// <summary>
/// Storefront product list with paging, search and category filter.
/// Reuses the existing <see cref="PagedResult{T}"/> shape from
/// <c>GetMembers</c> so the FE table component is shared.
/// </summary>
public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 24,
    string? Search = null,
    string? CategorySlug = null,
    bool IncludeInactive = false) : IRequest<PagedResult<ProductSummaryDto>>;

public sealed class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, PagedResult<ProductSummaryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetProductsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<ProductSummaryDto>> Handle(
        GetProductsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.PageSize is < 1 or > 100 ? 24 : request.PageSize;

        var q = from p in _db.Products.AsNoTracking()
                join c in _db.ProductCategories.AsNoTracking() on p.CategoryId equals c.Id
                select new { p, c };

        if (!request.IncludeInactive)
            q = q.Where(x => x.p.IsActive && x.c.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(x =>
                EF.Functions.Like(x.p.Name, $"%{term}%") ||
                EF.Functions.Like(x.p.Sku, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.CategorySlug))
        {
            var slug = request.CategorySlug.Trim().ToLowerInvariant();
            q = q.Where(x => x.c.Slug == slug);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderBy(x => x.c.DisplayOrder)
            .ThenBy(x => x.p.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new ProductSummaryDto(
                x.p.Id, x.p.Sku, x.p.Name, x.p.ImageUrl,
                x.p.PriceMinor, x.p.Currency, x.p.StockOnHand, x.p.IsActive,
                x.p.LowStockThreshold.HasValue && x.p.StockOnHand <= x.p.LowStockThreshold.Value,
                x.c.Id, x.c.Name))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductSummaryDto>(items, total, page, size);
    }
}
