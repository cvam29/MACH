namespace Mach.Infrastructure.Maps;

/// <summary>
/// Configuration for the Maps adapter, bound from the <c>Maps</c> section.
/// </summary>
public sealed class MapsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Maps";

    /// <summary>Which <c>IGeoLocator</c> implementation to register.</summary>
    public MapsProvider Provider { get; set; } = MapsProvider.Stub;

    /// <summary>Azure Maps subscription (shared) key.</summary>
    public string? SubscriptionKey { get; set; }

    /// <summary>Base URL for the Azure Maps REST endpoint.</summary>
    public string BaseUrl { get; set; } = "https://atlas.microsoft.com/";
}

/// <summary>Selects the geolocation backend.</summary>
public enum MapsProvider
{
    /// <summary>Deterministic offline geocoder. No network access.</summary>
    Stub = 0,

    /// <summary>Azure Maps Search/Geocoding REST API.</summary>
    Azure = 1,
}
