namespace Mach.Infrastructure.Maps;

using System.Security.Cryptography;
using System.Text;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;

/// <summary>
/// Deterministic, offline <see cref="IGeoLocator"/>. Maps an address to a stable
/// <see cref="GeoPoint"/> within a plausible land region by hashing its
/// city/postal-code/country, so distance-based delivery logic works with no network.
/// </summary>
public sealed class StubGeoLocator : IGeoLocator
{
    // A central-European-ish bounding box that keeps generated points on land
    // and in a sensible range for the demo (roughly France -> Poland, Italy -> Denmark).
    private const double MinLat = 43.0;
    private const double MaxLat = 55.0;
    private const double MinLng = 2.0;
    private const double MaxLng = 24.0;

    public Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken ct)
    {
        // Build a stable key from the locality-defining parts of the address.
        // Street is intentionally included so distinct addresses can differ, but the
        // dominant signal is city/postal/country so the same locality clusters together.
        var key = string.Join(
            '|',
            Normalize(address.Country),
            Normalize(address.City),
            Normalize(address.PostalCode),
            Normalize(address.Street));

        var point = Map(key);
        return Task.FromResult(Result.Success(point));
    }

    public Task<Result<double>> DistanceKmAsync(GeoPoint from, GeoPoint to, CancellationToken ct)
        => Task.FromResult(Result.Success(Haversine.DistanceKm(from, to)));

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static GeoPoint Map(string key)
    {
        // SHA-256 gives a stable, well-distributed hash across platforms/runs
        // (unlike string.GetHashCode, which is randomized per process).
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));

        var latFraction = ToUnitInterval(bytes, 0);
        var lngFraction = ToUnitInterval(bytes, 8);

        var lat = MinLat + (latFraction * (MaxLat - MinLat));
        var lng = MinLng + (lngFraction * (MaxLng - MinLng));

        return new GeoPoint(Math.Round(lat, 6), Math.Round(lng, 6));
    }

    private static double ToUnitInterval(byte[] hash, int offset)
    {
        // Take 8 bytes as an unsigned 64-bit value and scale into [0, 1).
        var value = BitConverter.ToUInt64(hash, offset);
        return value / (double)ulong.MaxValue;
    }
}
