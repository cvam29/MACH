using System.Text.Json;

using Mach.Application.Ports;
using Mach.Contracts;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Webhooks.Functions;

/// <summary>
/// Receives inbound commercetools subscription messages at <c>/api/hooks/commercetools</c>.
/// Validates the message minimally, maps to <see cref="ProductChanged"/> and publishes to
/// <see cref="Topics.Catalog"/>.
/// </summary>
public sealed class CommercetoolsWebhookFunctions(
    IMessageBus bus,
    ILogger<CommercetoolsWebhookFunctions> logger)
{
    [Function("CommercetoolsWebhook")]
    public async Task<IResult> Handle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "hooks/commercetools")] HttpRequest request,
        CancellationToken ct)
    {
        var rawBody = await WebhookRequest.ReadRawBodyAsync(request, ct).ConfigureAwait(false);

        if (!TryMap(rawBody, out var evt))
        {
            logger.LogWarning("commercetools webhook could not be mapped to a product change.");
            return Results.BadRequest(new { error = "Unrecognized or empty commercetools message." });
        }

        await bus.PublishAsync(Topics.Catalog, evt, ct).ConfigureAwait(false);
        logger.LogInformation(
            "Published product change {ProductId}/{Slug} ({Kind}).",
            evt.ProductId, evt.Slug, evt.Kind);

        return Results.Ok();
    }

    /// <summary>
    /// Maps a raw commercetools subscription delivery into a <see cref="ProductChanged"/> event.
    /// Returns false when the payload is empty or carries no product resource.
    /// </summary>
    public static bool TryMap(string rawBody, out ProductChanged evt)
    {
        evt = default!;
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            // Minimal validation: must reference a product resource.
            var typeId = GetNested(root, "resource", "typeId");
            if (!string.Equals(typeId, "product", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var productId =
                GetNested(root, "resource", "id")
                ?? GetString(root, "resourceId")
                ?? string.Empty;

            if (string.IsNullOrEmpty(productId))
            {
                return false;
            }

            var messageType = GetString(root, "type") ?? GetString(root, "notificationType");
            var slug =
                GetString(root, "slug")
                ?? GetNested(root, "productProjection", "slug")
                ?? string.Empty;

            evt = new ProductChanged(productId, slug, MapKind(messageType));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ProductChangeKind MapKind(string? messageType)
        => messageType switch
        {
            "ProductCreated" => ProductChangeKind.Created,
            "ProductDeleted" or "ResourceDeleted" => ProductChangeKind.Deleted,
            _ => ProductChangeKind.Updated,
        };

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var p)
            && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static string? GetNested(JsonElement element, string parent, string child)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(parent, out var p)
            ? GetString(p, child)
            : null;
}
