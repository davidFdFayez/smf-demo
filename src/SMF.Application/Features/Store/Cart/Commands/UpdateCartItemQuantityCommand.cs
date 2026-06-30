using FluentValidation;
using MediatR;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Store.Cart.Common;
using SMF.Application.Features.Store.Cart.Queries;
using SMF.Application.Features.Store.Common;

namespace SMF.Application.Features.Store.Cart.Commands;

public sealed record UpdateCartItemQuantityCommand(
    CartIdentity Identity,
    Guid ProductId,
    int Quantity) : IRequest<CartDto>;

public sealed class UpdateCartItemQuantityCommandValidator
    : AbstractValidator<UpdateCartItemQuantityCommand>
{
    public UpdateCartItemQuantityCommandValidator()
    {
        RuleFor(x => x.Identity).Must(i => i.IsValid);
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).LessThanOrEqualTo(50);
    }
}

public sealed class UpdateCartItemQuantityCommandHandler
    : IRequestHandler<UpdateCartItemQuantityCommand, CartDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IInvoicingSettings _settings;

    public UpdateCartItemQuantityCommandHandler(
        IApplicationDbContext db, IDateTimeProvider clock, IInvoicingSettings settings)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
    }

    public async Task<CartDto> Handle(
        UpdateCartItemQuantityCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartLookup.LoadAsync(_db, request.Identity, cancellationToken)
                   ?? throw new NotFoundException("Cart", request.Identity);

        cart.UpdateQuantity(request.ProductId, request.Quantity, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return await new GetCartQueryHandler(_db, _settings)
            .Handle(new GetCartQuery(request.Identity), cancellationToken);
    }
}

public sealed record RemoveCartItemCommand(CartIdentity Identity, Guid ProductId) : IRequest<CartDto>;

public sealed class RemoveCartItemCommandValidator : AbstractValidator<RemoveCartItemCommand>
{
    public RemoveCartItemCommandValidator()
    {
        RuleFor(x => x.Identity).Must(i => i.IsValid);
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
    }
}

public sealed class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, CartDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IInvoicingSettings _settings;

    public RemoveCartItemCommandHandler(
        IApplicationDbContext db, IDateTimeProvider clock, IInvoicingSettings settings)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
    }

    public async Task<CartDto> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartLookup.LoadAsync(_db, request.Identity, cancellationToken)
                   ?? throw new NotFoundException("Cart", request.Identity);
        cart.RemoveItem(request.ProductId, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return await new GetCartQueryHandler(_db, _settings)
            .Handle(new GetCartQuery(request.Identity), cancellationToken);
    }
}

public sealed record ClearCartCommand(CartIdentity Identity) : IRequest<CartDto>;

public sealed class ClearCartCommandValidator : AbstractValidator<ClearCartCommand>
{
    public ClearCartCommandValidator()
    {
        RuleFor(x => x.Identity).Must(i => i.IsValid);
    }
}

public sealed class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, CartDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IInvoicingSettings _settings;

    public ClearCartCommandHandler(
        IApplicationDbContext db, IDateTimeProvider clock, IInvoicingSettings settings)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
    }

    public async Task<CartDto> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartLookup.LoadAsync(_db, request.Identity, cancellationToken);
        if (cart is not null)
        {
            cart.Clear(_clock.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return await new GetCartQueryHandler(_db, _settings)
            .Handle(new GetCartQuery(request.Identity), cancellationToken);
    }
}
