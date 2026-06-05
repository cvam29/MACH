namespace Mach.Infrastructure.Maps.Tests;

using Mach.Domain.ValueObjects;
using Shouldly;
using Xunit;

public sealed class HaversineTests
{
    // Well-known city-centre coordinates.
    private static readonly GeoPoint Berlin = new(52.5200, 13.4050);
    private static readonly GeoPoint Munich = new(48.1351, 11.5820);
    private static readonly GeoPoint London = new(51.5074, -0.1278);
    private static readonly GeoPoint Paris = new(48.8566, 2.3522);

    [Fact]
    public void BerlinToMunich_IsAboutFiveHundredFourKm()
    {
        var km = Haversine.DistanceKm(Berlin, Munich);

        km.ShouldBe(504, tolerance: 10);
    }

    [Fact]
    public void LondonToParis_IsAboutThreeHundredFortyFourKm()
    {
        var km = Haversine.DistanceKm(London, Paris);

        km.ShouldBe(344, tolerance: 10);
    }

    [Fact]
    public void SamePoint_IsZero()
        => Haversine.DistanceKm(Berlin, Berlin).ShouldBe(0, tolerance: 1e-9);

    [Fact]
    public void IsSymmetric()
    {
        var ab = Haversine.DistanceKm(London, Paris);
        var ba = Haversine.DistanceKm(Paris, London);

        ba.ShouldBe(ab, tolerance: 1e-9);
    }
}
