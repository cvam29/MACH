namespace Mach.Persistence;

/// <summary>Database schema names, one per bounded concern (schema-per-concern).</summary>
internal static class MachSchemas
{
    public const string Messaging = "messaging";
    public const string Idempotency = "idempotency";
    public const string Orders = "orders";
    public const string Customers = "customers";
    public const string Fulfillment = "fulfillment";
    public const string Notifications = "notifications";
    public const string Audit = "audit";
}
