namespace Mach.Infrastructure.Maps;

using Mach.Domain.ValueObjects;

/// <summary>
/// Pure great-circle distance calculation shared by every <c>IGeoLocator</c> provider.
/// </summary>
public static class Haversine
{
    /// <summary>Mean Earth radius in kilometres (WGS-84 authalic sphere).</summary>
    public const double EarthRadiusKm = 6371.0088;

    /// <summary>
    /// Great-circle distance in kilometres between two WGS-84 coordinates.
    /// </summary>
    public static double DistanceKm(GeoPoint from, GeoPoint to)
    {
        var lat1 = ToRadians(from.Lat);
        var lat2 = ToRadians(to.Lat);
        var dLat = ToRadians(to.Lat - from.Lat);
        var dLng = ToRadians(to.Lng - from.Lng);

        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
              + (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2));

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
