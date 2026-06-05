namespace Mach.Persistence.Entities;

/// <summary>
/// Cached customer profile in <c>customers.ProfileCache</c>.
/// </summary>
public sealed class ProfileCacheEntity
{
    public string CustomerId { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? LoyaltyTier { get; set; }

    public DateTimeOffset RefreshedUtc { get; set; }
}
