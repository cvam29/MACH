using System.Text.Json;

using Mach.Application.Ports;
using Mach.Contracts;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mach.Webhooks.Functions;

/// <summary>
/// Receives inbound Contentstack content webhooks at <c>/api/hooks/contentstack</c>.
/// Authenticated by a shared secret header; maps to <see cref="ContentChanged"/> and publishes to
/// <see cref="Topics.Content"/>.
/// </summary>
public sealed class ContentstackWebhookFunctions(
    IMessageBus bus,
    IOptions<ContentstackWebhookOptions> options,
    ILogger<ContentstackWebhookFunctions> logger)
{
    /// <summary>Header carrying the configured shared secret on inbound Contentstack webhooks.</summary>
    public const string SecretHeader = "X-Contentstack-Webhook-Secret";

    private readonly ContentstackWebhookOptions _options = options.Value;

    [Function("ContentstackWebhook")]
    public async Task<IResult> Handle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "hooks/contentstack")] HttpRequest request,
        CancellationToken ct)
    {
        if (!SecretMatches(request))
        {
            logger.LogWarning("Rejected Contentstack webhook with missing/invalid shared secret.");
            return Results.Unauthorized();
        }

        var rawBody = await WebhookRequest.ReadRawBodyAsync(request, ct).ConfigureAwait(false);

        if (!TryMap(rawBody, out var evt))
        {
            // Authenticated but unusable payload: accept so Contentstack does not redeliver.
            logger.LogWarning("Contentstack webhook authenticated but could not be mapped.");
            return Results.Ok();
        }

        await bus.PublishAsync(Topics.Content, evt, ct).ConfigureAwait(false);
        logger.LogInformation(
            "Published content change {ContentType}/{Slug} ({Kind}).",
            evt.ContentType, evt.Slug, evt.Kind);

        return Results.Ok();
    }

    private bool SecretMatches(HttpRequest request)
    {
        var expected = _options.WebhookSecret;
        if (string.IsNullOrEmpty(expected))
        {
            // No secret configured ⇒ fail closed; never accept unauthenticated webhooks.
            return false;
        }

        if (!request.Headers.TryGetValue(SecretHeader, out var provided))
        {
            return false;
        }

        return FixedTimeEquals(provided.ToString(), expected);
    }

    /// <summary>Maps a raw Contentstack webhook body into a <see cref="ContentChanged"/> event.</summary>
    public static bool TryMap(string rawBody, out ContentChanged evt)
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

            var kind = MapKind(GetString(root, "event"));

            // data.content_type.{uid|title} and data.entry.{url|title|uid}
            var data = root.TryGetProperty("data", out var d) ? d : root;
            var contentType =
                GetNested(data, "content_type", "uid")
                ?? GetNested(data, "content_type", "title")
                ?? GetString(data, "content_type")
                ?? "unknown";

            var slug =
                GetNested(data, "entry", "url")
                ?? GetNested(data, "entry", "slug")
                ?? GetNested(data, "entry", "uid")
                ?? GetNested(data, "entry", "title")
                ?? string.Empty;

            evt = new ContentChanged(contentType, slug, kind);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ContentChangeKind MapKind(string? eventName)
        => eventName?.ToLowerInvariant() switch
        {
            "publish" or "entry.publish" or "entry_publish" => ContentChangeKind.Published,
            "unpublish" or "entry.unpublish" or "entry_unpublish" => ContentChangeKind.Unpublished,
            "delete" or "entry.delete" or "entry_delete" => ContentChangeKind.Deleted,
            _ => ContentChangeKind.Published,
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

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
