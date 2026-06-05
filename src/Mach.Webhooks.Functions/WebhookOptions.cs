namespace Mach.Webhooks.Functions;

/// <summary>
/// Shared-secret configuration for inbound vendor webhooks that authenticate with a static header
/// (Contentstack), bound from the <c>Contentstack:</c> configuration section.
/// </summary>
public sealed class ContentstackWebhookOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Contentstack";

    /// <summary>
    /// Shared secret expected on the <c>X-Contentstack-Webhook-Secret</c> header of inbound
    /// Contentstack webhooks. Configured via <c>Contentstack:WebhookSecret</c>.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
