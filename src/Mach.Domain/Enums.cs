namespace Mach.Domain;

/// <summary>Lifecycle status of an order.</summary>
public enum OrderStatus
{
    Pending,
    Paid,
    Fulfilling,
    Shipped,
    Delivered,
    Cancelled,
}

/// <summary>Status of a payment as reported by the payment gateway.</summary>
public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Refused,
    Refunded,
}

/// <summary>Delivery options offered at checkout. Same-day is distance-gated; store pickup is free.</summary>
public enum DeliveryType
{
    Standard,
    Express,
    SameDay,
    StorePickup,
}

/// <summary>The party a transactional notification is addressed to (multi-party fan-out).</summary>
public enum NotificationAudience
{
    Customer,
    Store,
    Supplier,
    Reception,
}
