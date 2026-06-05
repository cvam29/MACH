namespace Mach.Infrastructure.Maps;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;

/// <summary>
/// <see cref="IGeoLocator"/> backed by the Azure Maps "Get Geocoding" REST API.
/// Distance uses the shared <see cref="Haversine"/> helper (no extra API call).
/// </summary>
public sealed class AzureMapsGeoLocator : IGeoLocator
{
    /// <summary>Azure Maps Geocoding API version targeted by this adapter.</summary>
    public const string ApiVersion = "2023-06-01";

    private readonly HttpClient _http;

    public AzureMapsGeoLocator(HttpClient http) => _http = http;

    public async Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken ct)
    {
        var query = BuildQuery(address);
        if (string.IsNullOrWhiteSpace(query))
        {
            return Error.Validation("Address has no geocodable content.");
        }

        var requestUri =
            $"geocode?api-version={ApiVersion}&query={Uri.EscapeDataString(query)}";

        GeocodingResponse? payload;
        try
        {
            using var response = await _http.GetAsync(requestUri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Error.Unexpected(
                    $"Azure Maps geocoding failed with status {(int)response.StatusCode}.");
            }

            payload = await response.Content
                .ReadFromJsonAsync<GeocodingResponse>(ct)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return Error.Unexpected($"Azure Maps geocoding request error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error.Unexpected("Azure Maps geocoding request timed out.");
        }

        var coordinates = payload?.Features is { Count: > 0 }
            ? payload.Features[0].Geometry?.Coordinates
            : null;

        // GeoJSON coordinate order is [longitude, latitude].
        if (coordinates is not { Length: >= 2 })
        {
            return Error.NotFound($"No geocoding result for '{query}'.");
        }

        return new GeoPoint(Lat: coordinates[1], Lng: coordinates[0]);
    }

    public Task<Result<double>> DistanceKmAsync(GeoPoint from, GeoPoint to, CancellationToken ct)
        => Task.FromResult(Result.Success(Haversine.DistanceKm(from, to)));

    private static string BuildQuery(Address address)
    {
        // A free-form single-line query is the most reliable input to the geocoder.
        var parts = new[]
        {
            address.Street,
            address.City,
            address.State,
            address.PostalCode,
            address.Country,
        };

        return string.Join(
            ", ",
            parts
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Select(static p => p!.Trim()));
    }

    // --- GeoJSON response DTOs (only the fields we need) ---

    private sealed record GeocodingResponse(
        [property: JsonPropertyName("features")] IReadOnlyList<Feature>? Features);

    private sealed record Feature(
        [property: JsonPropertyName("geometry")] Geometry? Geometry);

    private sealed record Geometry(
        [property: JsonPropertyName("coordinates")] double[]? Coordinates);
}
