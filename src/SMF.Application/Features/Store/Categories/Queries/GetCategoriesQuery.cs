using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Store.Common;

namespace SMF.Application.Features.Store.Categories.Queries;

public sealed record GetCategoriesQuery(bool IncludeInactive = false)
    : IRequest<IReadOnlyCollection<CategoryDto>>;

public sealed class GetCategoriesQueryHandler
    : IRequestHandler<GetCategoriesQuery, IReadOnlyCollection<CategoryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<CategoryDto>> Handle(
        GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ProductCategories.AsNoTracking();
        if (!request.IncludeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id, c.Name, c.Slug, c.Description, c.ImageUrl, c.DisplayOrder, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}
