using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Ports;

/// <summary>
/// Port over geocoding + distance (Azure Maps, or an offline haversine stub).
/// Implemented by <c>Mach.Infrastructure.Maps</c>.
/// </summary>
public interface IGeoLocator
{
    Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken ct);

    /// <summary>Great-circle / route distance in kilometres between two points.</summary>
    Task<Result<double>> DistanceKmAsync(GeoPoint from, GeoPoint to, CancellationToken ct);
}
