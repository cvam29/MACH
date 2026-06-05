namespace Mach.Bff.Functions;

/// <summary>
/// CORS configuration, bound from the <c>Cors:</c> configuration section. The storefront origin(s)
/// are allowed with credentials so the browser sends/receives the httpOnly session cookies.
/// </summary>
public sealed class CorsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>Allowed storefront origins. Defaults to the local Next.js dev server.</summary>
    public string[] AllowedOrigins { get; set; } = ["http://localhost:3000"];
}
