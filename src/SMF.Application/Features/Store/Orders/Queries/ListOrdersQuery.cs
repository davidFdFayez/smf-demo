using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Members.Queries.GetMembers;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Store.Orders.Queries;

public sealed record ListOrdersQuery(
    int Page = 1,
    int PageSize = 25,
    OrderStatus? Status = null,
    Guid? MemberId = null,
    string? Search = null) : IRequest<PagedResult<OrderListItem>>;

public sealed record OrderListItem(
    Guid Id,
    string OrderNumber,
    Guid? MemberId,
    string BuyerName,
    string BuyerEmail,
    long TotalMinor,
    string Currency,
    OrderStatus Status,
    int ItemCount,
    DateTime CreatedAtUtc);

public sealed class ListOrdersQueryHandler
    : IRequestHandler<ListOrdersQuery, PagedResult<OrderListItem>>
{
    private readonly IApplicationDbContext _db;

    public ListOrdersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<OrderListItem>> Handle(
        ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var q = _db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
        if (request.Status is { } st) q = q.Where(o => o.Status == st);
        if (request.MemberId is { } mid && mid != Guid.Empty) q = q.Where(o => o.MemberId == mid);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(o =>
                EF.Functions.Like(o.OrderNumber, $"%{term}%") ||
                EF.Functions.Like(o.BuyerName, $"%{term}%") ||
                EF.Functions.Like(o.BuyerEmail, $"%{term}%"));
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(o => new OrderListItem(
                o.Id, o.OrderNumber, o.MemberId, o.BuyerName, o.BuyerEmail,
                o.TotalMinor, o.Currency, o.Status, o.Items.Count, o.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderListItem>(items, total, page, size);
    }
}
