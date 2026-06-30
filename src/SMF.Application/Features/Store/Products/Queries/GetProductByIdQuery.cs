using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Store.Common;

namespace SMF.Application.Features.Store.Products.Queries;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductDetailDto>;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetProductByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDetailDto> Handle(
        GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var row = await (from p in _db.Products.AsNoTracking()
                         join c in _db.ProductCategories.AsNoTracking() on p.CategoryId equals c.Id
                         where p.Id == request.Id
                         select new ProductDetailDto(
                             p.Id, p.Sku, p.Name, p.Description, p.ImageUrl,
                             p.PriceMinor, p.Currency, p.StockOnHand, p.LowStockThreshold,
                             p.IsActive, c.Id, c.Name, c.Slug))
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? throw new NotFoundException("Product", request.Id);
    }
}
