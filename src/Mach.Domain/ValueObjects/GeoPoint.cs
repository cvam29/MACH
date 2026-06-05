namespace Mach.Domain.ValueObjects;

/// <summary>
/// A WGS-84 geographic coordinate (decimal degrees).
/// </summary>
public readonly record struct GeoPoint(double Lat, double Lng);
