namespace Mach.Notifications.Functions;

/// <summary>
/// Configuration for the notifications host, bound from the <c>Notifications:</c> section.
/// Store / supplier / reception recipients are resolved from SQL at runtime; these are the
/// config fallbacks (chiefly the customer fallback when an order carries no customer email).
/// </summary>
public sealed class NotificationOptions
{
    /// <summary>The configuration section name these options bind to.</summary>
    public const string SectionName = "Notifications";

    /// <summary>
    /// Fallback recipient address used for the customer audience when the order/customer
    /// lookup yields no email (anonymous checkout, missing profile, or commerce read failure).
    /// </summary>
    public string? Recipient { get; set; }
}
