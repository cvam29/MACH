namespace Mach.Contracts;

/// <summary>Topic names used on the message bus.</summary>
public static class Topics
{
    public const string Payments = "payments";
    public const string Catalog = "catalog";
    public const string Content = "content";
    public const string Notifications = "notifications";
}

/// <summary>An order was placed; drives projection + notification fan-out.</summary>
public sealed record OrderPlaced(
    string OrderId,
    string OrderNumber,
    string? CustomerId,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderPlacedLine> Lines) : IntegrationEvent
{
    public override string EventType => "order.placed";
}

/// <summary>A line on an <see cref="OrderPlaced"/> event.</summary>
public sealed record OrderPlacedLine(string Sku, int Quantity);

/// <summary>A payment-gateway notification was received and verified.</summary>
public sealed record PaymentNotificationReceived(
    string PspReference,
    string MerchantReference,
    string EventCode,
    bool Success,
    decimal Amount,
    string Currency) : IntegrationEvent
{
    public override string EventType => "payment.notification.received";
}

/// <summary>A product changed in the commerce engine; drives reindex.</summary>
public sealed record ProductChanged(
    string ProductId,
    string Slug,
    ProductChangeKind Kind) : IntegrationEvent
{
    public override string EventType => "product.changed";
}

/// <summary>The kind of change applied to a product.</summary>
public enum ProductChangeKind
{
    Created,
    Updated,
    Deleted,
}

/// <summary>A CMS content entry changed; may trigger cache invalidation / reindex.</summary>
public sealed record ContentChanged(
    string ContentType,
    string Slug,
    ContentChangeKind Kind) : IntegrationEvent
{
    public override string EventType => "content.changed";
}

/// <summary>The kind of change applied to a content entry.</summary>
public enum ContentChangeKind
{
    Published,
    Unpublished,
    Deleted,
}
