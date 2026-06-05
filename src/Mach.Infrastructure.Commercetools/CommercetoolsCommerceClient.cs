using commercetools.Sdk.Api.Client;
using commercetools.Sdk.Api.Models.Carts;
using commercetools.Sdk.Api.Models.Common;
using commercetools.Sdk.Api.Models.Orders;
using commercetools.Sdk.Api.Models.ShippingMethods;

using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;

using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// commercetools-backed implementation of <see cref="ICommerceClient"/>. Uses the generated
/// request-builder DSL over a client-credentials-authenticated <see cref="IClient"/>, mapping
/// API models to application DTOs and translating transport faults to <see cref="Result"/> failures.
/// </summary>
public sealed class CommercetoolsCommerceClient : ICommerceClient
{
    private readonly ProjectApiRoot _builder;
    private readonly CommercetoolsMapper _mapper;
    private readonly string _locale;

    public CommercetoolsCommerceClient(
        ProjectApiRoot builder,
        IOptions<CommercetoolsOptions> options)
    {
        _builder = builder;
        _locale = options.Value.DefaultLocale;
        _mapper = new CommercetoolsMapper(_locale);
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(CancellationToken ct)
    {
        try
        {
            var response = await _builder.Categories().Get().WithLimit(500).ExecuteAsync(ct);
            IReadOnlyList<CategoryDto> categories = (response.Results ?? [])
                .Select(_mapper.MapCategory)
                .ToList();
            return Result.Success(categories);
        }
        catch (Exception ex)
        {
            return Result.Failure<IReadOnlyList<CategoryDto>>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public async Task<Result<ProductDto>> GetProductBySlugAsync(string slug, CancellationToken ct)
    {
        try
        {
            // Escape embedded quotes to keep the predicate well-formed.
            var safeSlug = slug.Replace("\"", "\\\"");
            var response = await _builder.ProductProjections()
                .Get()
                .WithWhere($"slug({_locale}=\"{safeSlug}\")")
                .WithLimit(1)
                .ExecuteAsync(ct);

            var product = (response.Results ?? []).FirstOrDefault();
            if (product is null)
            {
                return Result.Failure<ProductDto>(Error.NotFound($"No product with slug '{slug}'."));
            }

            return Result.Success(_mapper.MapProduct(product));
        }
        catch (Exception ex)
        {
            return Result.Failure<ProductDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public async Task<Result<CartDto>> CreateCartAsync(string currency, string? anonymousId, CancellationToken ct)
    {
        try
        {
            var draft = new CartDraft { Currency = currency };
            if (!string.IsNullOrEmpty(anonymousId))
            {
                draft.AnonymousId = anonymousId;
            }

            var cart = await _builder.Carts().Post(draft).ExecuteAsync(ct);
            return Result.Success(_mapper.MapCart(cart));
        }
        catch (Exception ex)
        {
            return Result.Failure<CartDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public async Task<Result<CartDto>> GetCartAsync(CartId cartId, CancellationToken ct)
    {
        try
        {
            var cart = await _builder.Carts().WithId(cartId.Value).Get().ExecuteAsync(ct);
            return Result.Success(_mapper.MapCart(cart));
        }
        catch (Exception ex)
        {
            return Result.Failure<CartDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public Task<Result<CartDto>> AddLineItemAsync(
        CartId cartId, long version, AddLineItemRequest request, CancellationToken ct)
    {
        var action = new CartAddLineItemAction
        {
            Sku = request.Sku.Value,
            Quantity = request.Quantity,
        };
        return UpdateCartAsync(cartId, version, action, ct);
    }

    public Task<Result<CartDto>> UpdateLineItemQuantityAsync(
        CartId cartId, long version, string lineItemId, int quantity, CancellationToken ct)
    {
        var action = new CartChangeLineItemQuantityAction
        {
            LineItemId = lineItemId,
            Quantity = quantity,
        };
        return UpdateCartAsync(cartId, version, action, ct);
    }

    public Task<Result<CartDto>> RemoveLineItemAsync(
        CartId cartId, long version, string lineItemId, CancellationToken ct)
    {
        var action = new CartRemoveLineItemAction { LineItemId = lineItemId };
        return UpdateCartAsync(cartId, version, action, ct);
    }

    public Task<Result<CartDto>> SetCartAddressesAsync(
        CartId cartId, long version, Address? shipping, Address? billing, CancellationToken ct)
    {
        var actions = new List<ICartUpdateAction>();
        if (shipping is { } s)
        {
            actions.Add(new CartSetShippingAddressAction { Address = CommercetoolsMapper.MapAddress(s) });
        }

        if (billing is { } b)
        {
            actions.Add(new CartSetBillingAddressAction { Address = CommercetoolsMapper.MapAddress(b) });
        }

        if (actions.Count == 0)
        {
            return GetCartAsync(cartId, ct);
        }

        return UpdateCartAsync(cartId, version, actions, ct);
    }

    public Task<Result<CartDto>> SetShippingMethodAsync(
        CartId cartId, long version, ShippingSelection selection, CancellationToken ct)
    {
        // External price: model the selection as a custom shipping method carrying the
        // distance-computed price, while preserving the chosen method id as its name reference.
        var price = ToMoney(selection.ExternalPrice);
        var action = new CartSetCustomShippingMethodAction
        {
            ShippingMethodName = selection.ShippingMethodId,
            ShippingRate = new ShippingRateDraft { Price = price },
        };

        return UpdateCartAsync(cartId, version, action, ct);
    }

    public async Task<Result<OrderDto>> CreateOrderFromCartAsync(CartId cartId, long version, CancellationToken ct)
    {
        try
        {
            var draft = new OrderFromCartDraft
            {
                Cart = new CartResourceIdentifier { Id = cartId.Value },
                Version = version,
            };
            var order = await _builder.Orders().Post(draft).ExecuteAsync(ct);
            return Result.Success(_mapper.MapOrder(order));
        }
        catch (Exception ex)
        {
            return Result.Failure<OrderDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public async Task<Result<OrderDto>> GetOrderAsync(OrderId orderId, CancellationToken ct)
    {
        try
        {
            var order = await _builder.Orders().WithId(orderId.Value).Get().ExecuteAsync(ct);
            return Result.Success(_mapper.MapOrder(order));
        }
        catch (Exception ex)
        {
            return Result.Failure<OrderDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public async Task<Result<OrderDto>> TransitionOrderAsync(
        OrderId orderId, OrderStatus status, CancellationToken ct)
    {
        try
        {
            // The contract gives no version, so read the current order to obtain it.
            var current = await _builder.Orders().WithId(orderId.Value).Get().ExecuteAsync(ct);
            var actions = MapStatusToActions(status);

            var update = new OrderUpdate { Version = current.Version, Actions = actions };
            var order = await _builder.Orders().WithId(orderId.Value).Post(update).ExecuteAsync(ct);
            return Result.Success(_mapper.MapOrder(order));
        }
        catch (Exception ex)
        {
            return Result.Failure<OrderDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public async Task<Result<CustomerDto>> GetCustomerAsync(CustomerId customerId, CancellationToken ct)
    {
        try
        {
            var customer = await _builder.Customers().WithId(customerId.Value).Get().ExecuteAsync(ct);
            return Result.Success(_mapper.MapCustomer(customer));
        }
        catch (Exception ex)
        {
            return Result.Failure<CustomerDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    private Task<Result<CartDto>> UpdateCartAsync(
        CartId cartId, long version, ICartUpdateAction action, CancellationToken ct)
        => UpdateCartAsync(cartId, version, [action], ct);

    private async Task<Result<CartDto>> UpdateCartAsync(
        CartId cartId, long version, IList<ICartUpdateAction> actions, CancellationToken ct)
    {
        try
        {
            var update = new CartUpdate { Version = version, Actions = actions };
            var cart = await _builder.Carts().WithId(cartId.Value).Post(update).ExecuteAsync(ct);
            return Result.Success(_mapper.MapCart(cart));
        }
        catch (Exception ex)
        {
            return Result.Failure<CartDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    /// <summary>
    /// Map the domain <see cref="OrderStatus"/> onto commercetools order/shipment/payment-state
    /// transition actions.
    /// </summary>
    private static List<IOrderUpdateAction> MapStatusToActions(OrderStatus status) => status switch
    {
        OrderStatus.Pending =>
            [new OrderChangeOrderStateAction { OrderState = IOrderState.Open }],

        OrderStatus.Paid =>
        [
            new OrderChangePaymentStateAction { PaymentState = IPaymentState.Paid },
        ],

        OrderStatus.Fulfilling =>
        [
            new OrderChangeOrderStateAction { OrderState = IOrderState.Confirmed },
        ],

        OrderStatus.Shipped =>
        [
            new OrderChangeShipmentStateAction { ShipmentState = IShipmentState.Shipped },
        ],

        OrderStatus.Delivered =>
        [
            new OrderChangeShipmentStateAction { ShipmentState = IShipmentState.Delivered },
            new OrderChangeOrderStateAction { OrderState = IOrderState.Complete },
        ],

        OrderStatus.Cancelled =>
            [new OrderChangeOrderStateAction { OrderState = IOrderState.Cancelled }],

        _ => [new OrderChangeOrderStateAction { OrderState = IOrderState.Open }],
    };

    private static CtMoney ToMoney(Money money)
    {
        // commercetools cent precision uses 2 fraction digits for the supported demo currencies.
        var centAmount = (long)Math.Round(money.Amount * 100m, MidpointRounding.AwayFromZero);
        return new CtMoney { CentAmount = centAmount, CurrencyCode = money.Currency };
    }
}
