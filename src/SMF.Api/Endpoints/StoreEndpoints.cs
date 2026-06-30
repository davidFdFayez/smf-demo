using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Members.Queries.GetMembers;
using SMF.Application.Features.Store.Cart.Commands;
using SMF.Application.Features.Store.Cart.Common;
using SMF.Application.Features.Store.Cart.Queries;
using SMF.Application.Features.Store.Categories.Commands;
using SMF.Application.Features.Store.Categories.Queries;
using SMF.Application.Features.Store.Checkout.Commands;
using SMF.Application.Features.Store.Common;
using SMF.Application.Features.Store.Orders.Commands;
using SMF.Application.Features.Store.Orders.Queries;
using SMF.Application.Features.Store.Products.Commands;
using SMF.Application.Features.Store.Products.Queries;
using SMF.Domain.Enums;

namespace SMF.Api.Endpoints;

/// <summary>
/// Storefront API:
///
///   * <c>/api/store/categories</c> + <c>/api/store/products</c> — public catalog reads.
///   * <c>/api/store/cart</c> — cart manipulation, keyed by <c>X-Cart-Key</c>
///     (guest UUID held in localStorage on the client) or by an authenticated
///     member id.
///   * <c>/api/store/checkout</c> — converts the cart into an order and
///     returns the gateway redirect URL.
///   * <c>/api/store/orders/{id}</c> — fetch order detail.
///   * <c>/api/store/admin/...</c> — admin product + order management.
/// </summary>
public static class StoreEndpoints
{
    private const string CartKeyHeader = "X-Cart-Key";

    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("/api/store").WithTags("Store");

        // --- Categories -----------------------------------------------------
        pub.MapGet("/categories", async (
                ISender sender, CancellationToken ct,
                [FromQuery] bool includeInactive = false) =>
                Results.Ok(await sender.Send(new GetCategoriesQuery(includeInactive), ct)))
           .WithName("ListCategories")
           .Produces<IReadOnlyCollection<CategoryDto>>(StatusCodes.Status200OK);

        // --- Catalog --------------------------------------------------------
        pub.MapGet("/products", async (
                ISender sender, CancellationToken ct,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 24,
                [FromQuery] string? search = null,
                [FromQuery] string? category = null) =>
                Results.Ok(await sender.Send(
                    new GetProductsQuery(page, pageSize, search, category), ct)))
           .WithName("ListProducts")
           .Produces<PagedResult<ProductSummaryDto>>(StatusCodes.Status200OK);

        pub.MapGet("/products/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetProductByIdQuery(id), ct)))
           .WithName("GetProductById")
           .Produces<ProductDetailDto>(StatusCodes.Status200OK)
           .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Cart -----------------------------------------------------------
        pub.MapGet("/cart", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetCartQuery(ResolveIdentity(ctx)), ct)))
           .WithName("GetCart")
           .Produces<CartDto>(StatusCodes.Status200OK);

        pub.MapPost("/cart/items", async (
                HttpContext ctx,
                [FromBody] AddItemRequest req,
                ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new AddCartItemCommand(ResolveIdentity(ctx), req.ProductId, req.Quantity), ct)))
           .WithName("AddCartItem")
           .Produces<CartDto>(StatusCodes.Status200OK)
           .ProducesValidationProblem();

        pub.MapPatch("/cart/items/{productId:guid}", async (
                Guid productId, HttpContext ctx,
                [FromBody] UpdateQuantityRequest req,
                ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new UpdateCartItemQuantityCommand(ResolveIdentity(ctx), productId, req.Quantity), ct)))
           .WithName("UpdateCartItem")
           .Produces<CartDto>(StatusCodes.Status200OK);

        pub.MapDelete("/cart/items/{productId:guid}", async (
                Guid productId, HttpContext ctx, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(
                    new RemoveCartItemCommand(ResolveIdentity(ctx), productId), ct)))
           .WithName("RemoveCartItem")
           .Produces<CartDto>(StatusCodes.Status200OK);

        pub.MapDelete("/cart", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ClearCartCommand(ResolveIdentity(ctx)), ct)))
           .WithName("ClearCart")
           .Produces<CartDto>(StatusCodes.Status200OK);

        // --- Checkout -------------------------------------------------------
        pub.MapPost("/checkout", async (
                HttpContext ctx,
                [FromBody] CheckoutRequest req,
                ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CheckoutCartCommand(
                    ResolveIdentity(ctx),
                    req.BuyerName,
                    req.BuyerEmail,
                    req.BuyerTaxNumber,
                    req.ShippingAddress,
                    req.ShippingFeeMinor,
                    req.Provider,
                    req.CallbackUrl,
                    req.MemberId), ct);
                return Results.Ok(result);
            })
           .WithName("CheckoutCart")
           .Produces<CheckoutResultDto>(StatusCodes.Status200OK)
           .ProducesValidationProblem();

        // --- Orders ---------------------------------------------------------
        pub.MapGet("/orders/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetOrderQuery(id), ct)))
           .WithName("GetOrder")
           .Produces<OrderDto>(StatusCodes.Status200OK)
           .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Admin ----------------------------------------------------------
        var admin = app.MapGroup("/api/admin/store").WithTags("Store (Admin)");

        admin.MapPost("/categories", async (
                [FromBody] CreateCategoryCommand cmd, ISender sender, CancellationToken ct) =>
            {
                var id = await sender.Send(cmd, ct);
                return Results.Created($"/api/admin/store/categories/{id}", new { id });
            })
            .WithName("AdminCreateCategory")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        admin.MapPost("/products", async (
                [FromBody] CreateProductCommand cmd, ISender sender, CancellationToken ct) =>
            {
                var id = await sender.Send(cmd, ct);
                return Results.Created($"/api/store/products/{id}", new { id });
            })
            .WithName("AdminCreateProduct")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        admin.MapPut("/products/{id:guid}", async (
                Guid id, [FromBody] UpdateProductBody body, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateProductCommand(
                    id, body.CategoryId, body.Name, body.Description, body.ImageUrl,
                    body.PriceMinor, body.LowStockThreshold, body.IsActive), ct);
                return Results.NoContent();
            })
            .WithName("AdminUpdateProduct")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/products/{id:guid}/stock", async (
                Guid id, [FromBody] AdjustStockBody body, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new AdjustStockCommand(id, body.Delta, body.Reason), ct);
                return Results.NoContent();
            })
            .WithName("AdminAdjustStock")
            .Produces(StatusCodes.Status204NoContent);

        admin.MapGet("/orders", async (
                ISender sender, CancellationToken ct,
                [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
                [FromQuery] OrderStatus? status = null,
                [FromQuery] Guid? memberId = null,
                [FromQuery] string? search = null) =>
                Results.Ok(await sender.Send(
                    new ListOrdersQuery(page, pageSize, status, memberId, search), ct)))
            .WithName("AdminListOrders")
            .Produces<PagedResult<OrderListItem>>(StatusCodes.Status200OK);

        admin.MapPost("/orders/{id:guid}/fulfill", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new FulfillOrderCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("AdminFulfillOrder")
            .Produces(StatusCodes.Status204NoContent);

        admin.MapPost("/orders/{id:guid}/cancel", async (
                Guid id, [FromBody] CancelOrderBody? body, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new CancelOrderCommand(id, body?.Reason), ct);
                return Results.NoContent();
            })
            .WithName("AdminCancelOrder")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static CartIdentity ResolveIdentity(HttpContext ctx)
    {
        Guid? guestKey = null;
        if (ctx.Request.Headers.TryGetValue(CartKeyHeader, out var raw)
            && Guid.TryParse(raw.ToString(), out var parsed))
            guestKey = parsed;

        Guid? memberId = null;
        var nameId = ctx.User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(nameId) && Guid.TryParse(nameId, out var memberGuid))
            memberId = memberGuid;

        return new CartIdentity(guestKey, memberId);
    }

    public sealed record AddItemRequest(Guid ProductId, int Quantity);
    public sealed record UpdateQuantityRequest(int Quantity);

    public sealed record CheckoutRequest(
        string BuyerName,
        string BuyerEmail,
        string? BuyerTaxNumber,
        OrderShippingAddressDto ShippingAddress,
        long ShippingFeeMinor,
        PaymentProvider Provider,
        string CallbackUrl,
        Guid? MemberId = null);

    public sealed record UpdateProductBody(
        Guid CategoryId,
        string Name,
        string? Description,
        string? ImageUrl,
        long PriceMinor,
        int? LowStockThreshold,
        bool IsActive);

    public sealed record AdjustStockBody(int Delta, string? Reason = null);
    public sealed record CancelOrderBody(string? Reason = null);
}
