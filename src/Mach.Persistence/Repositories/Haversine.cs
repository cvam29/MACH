using Mach.Domain.ValueObjects;

namespace Mach.Persistence.Repositories;

/// <summary>
/// Great-circle distance between two WGS-84 coordinates using the haversine formula.
/// Pure math — deliberately self-contained so persistence has no dependency on the
/// Maps project. Used to pick the nearest fulfilling store.
/// </summary>
internal static class Haversine
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>Great-circle distance between <paramref name="a"/> and <paramref name="b"/> in kilometres.</summary>
    public static double DistanceKm(GeoPoint a, GeoPoint b)
    {
        var lat1 = ToRadians(a.Lat);
        var lat2 = ToRadians(b.Lat);
        var dLat = ToRadians(b.Lat - a.Lat);
        var dLng = ToRadians(b.Lng - a.Lng);

        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2));

        var c = 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
