using System.Net;
using System.Text.Json;
using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Contentstack;

/// <summary>
/// <see cref="ICmsClient"/> implemented over the Contentstack Content Delivery API (CDA)
/// using a typed <see cref="HttpClient"/> (no vendor SDK). Maps CDA JSON to application CMS DTOs.
/// </summary>
public sealed class ContentstackCmsClient : ICmsClient
{
    private const string NavigationContentType = "navigation";
    private const string EmailTemplateContentType = "email_template";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ContentstackOptions _options;

    public ContentstackCmsClient(HttpClient httpClient, IOptions<ContentstackOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result<ContentEntryDto>> GetEntryAsync(
        string contentType, string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return Result.Failure<ContentEntryDto>(Error.Validation("Content type is required."));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<ContentEntryDto>(Error.Validation("Slug is required."));
        }

        // Entries store their public path in the built-in `url` field. We normalise the slug
        // to a `/`-prefixed path and query by it.
        var url = slug.StartsWith('/') ? slug : "/" + slug;
        var query = BuildQuery(("url", url));
        var requestUri = BuildEntriesUri(contentType, query);

        var (response, error) = await SendAsync(requestUri, ct).ConfigureAwait(false);
        if (error is not null)
        {
            return Result.Failure<ContentEntryDto>(error);
        }

        var entry = response!.Entries.Count > 0 ? response.Entries[0] : (JsonElement?)null;
        if (entry is null)
        {
            return Result.Failure<ContentEntryDto>(
                Error.NotFound($"No '{contentType}' entry found for slug '{slug}'."));
        }

        return Result.Success(MapEntry(contentType, entry.Value));
    }

    /// <inheritdoc />
    public async Task<Result<NavigationNodeDto>> GetNavigationAsync(CancellationToken ct)
    {
        var requestUri = BuildEntriesUri(NavigationContentType, query: null);

        var (response, error) = await SendAsync(requestUri, ct).ConfigureAwait(false);
        if (error is not null)
        {
            return Result.Failure<NavigationNodeDto>(error);
        }

        var entry = response!.Entries.Count > 0 ? response.Entries[0] : (JsonElement?)null;
        if (entry is null)
        {
            return Result.Failure<NavigationNodeDto>(
                Error.NotFound("No navigation entry found."));
        }

        return Result.Success(MapNavigation(entry.Value));
    }

    /// <inheritdoc />
    public async Task<Result<EmailTemplateDto>> GetEmailTemplateAsync(
        NotificationAudience audience, CancellationToken ct)
    {
        var audienceToken = audience.ToString().ToLowerInvariant();
        var query = BuildQuery(("audience", audienceToken));
        var requestUri = BuildEntriesUri(EmailTemplateContentType, query);

        var (response, error) = await SendAsync(requestUri, ct).ConfigureAwait(false);
        if (error is not null)
        {
            return Result.Failure<EmailTemplateDto>(error);
        }

        var entry = response!.Entries.Count > 0 ? response.Entries[0] : (JsonElement?)null;
        if (entry is null)
        {
            return Result.Failure<EmailTemplateDto>(
                Error.NotFound($"No email template found for audience '{audienceToken}'."));
        }

        var subject = GetString(entry.Value, "subject") ?? string.Empty;
        var body = GetString(entry.Value, "body") ?? string.Empty;
        return Result.Success(new EmailTemplateDto(audience, subject, body));
    }

    private async Task<(EntriesResponse? Response, Error? Error)> SendAsync(
        string requestUri, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return (null, Error.Unexpected($"Contentstack request failed: {ex.Message}"));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return (null, Error.Unexpected($"Contentstack request timed out: {ex.Message}"));
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (new EntriesResponse(), null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (null, Error.Unexpected(
                    $"Contentstack returned {(int)response.StatusCode} ({response.ReasonPhrase})."));
            }

            try
            {
                await using var stream = await response.Content
                    .ReadAsStreamAsync(ct).ConfigureAwait(false);
                var parsed = await JsonSerializer
                    .DeserializeAsync<EntriesResponse>(stream, JsonOptions, ct)
                    .ConfigureAwait(false);
                return (parsed ?? new EntriesResponse(), null);
            }
            catch (JsonException ex)
            {
                return (null, Error.Unexpected($"Failed to parse Contentstack response: {ex.Message}"));
            }
        }
    }

    private string BuildEntriesUri(string contentType, string? query)
    {
        var baseUrl = _options.ResolveBaseUrl();
        var builder = new System.Text.StringBuilder();
        builder.Append(baseUrl)
            .Append("/v3/content_types/")
            .Append(Uri.EscapeDataString(contentType))
            .Append("/entries?environment=")
            .Append(Uri.EscapeDataString(_options.Environment))
            .Append("&locale=")
            .Append(Uri.EscapeDataString(_options.Locale))
            .Append("&limit=1");

        if (!string.IsNullOrEmpty(query))
        {
            builder.Append("&query=").Append(Uri.EscapeDataString(query));
        }

        return builder.ToString();
    }

    private static string BuildQuery(params (string Field, string Value)[] terms)
    {
        var map = new Dictionary<string, string>(terms.Length);
        foreach (var (field, value) in terms)
        {
            map[field] = value;
        }

        return JsonSerializer.Serialize(map, JsonOptions);
    }

    private static ContentEntryDto MapEntry(string contentType, JsonElement entry)
    {
        var slug = GetString(entry, "url") ?? string.Empty;
        var title = GetString(entry, "title") ?? string.Empty;

        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in entry.EnumerateObject())
        {
            fields[property.Name] = ToClrValue(property.Value);
        }

        return new ContentEntryDto(contentType, slug, title, fields);
    }

    private static NavigationNodeDto MapNavigation(JsonElement entry)
    {
        var label = GetString(entry, "title") ?? string.Empty;
        var url = GetString(entry, "url") ?? string.Empty;

        var children = new List<NavigationNodeDto>();
        if (entry.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                children.Add(MapNavigationNode(item));
            }
        }

        return new NavigationNodeDto(label, url, children);
    }

    private static NavigationNodeDto MapNavigationNode(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return new NavigationNodeDto(string.Empty, string.Empty, []);
        }

        var label = GetString(node, "label") ?? GetString(node, "title") ?? string.Empty;
        var url = GetString(node, "url") ?? string.Empty;

        var children = new List<NavigationNodeDto>();
        if (node.TryGetProperty("children", out var nested) && nested.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in nested.EnumerateArray())
            {
                children.Add(MapNavigationNode(child));
            }
        }

        return new NavigationNodeDto(label, url, children);
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ToClrValue).ToList(),
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => ToClrValue(p.Value), StringComparer.Ordinal),
        _ => null,
    };
}
