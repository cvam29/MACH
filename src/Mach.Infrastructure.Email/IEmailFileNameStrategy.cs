using Mach.Application.Dtos;

namespace Mach.Infrastructure.Email;

/// <summary>
/// Produces a deterministic file name for a dev-sink email. Abstracted so tests can avoid
/// <c>DateTime.Now</c> and assert exact, stable file names.
/// </summary>
public interface IEmailFileNameStrategy
{
    /// <summary>Returns the <c>.eml</c> file name (no directory) for the given message.</summary>
    string GetFileName(EmailMessage message);
}

/// <summary>
/// Default strategy: <c>{audience}-{8-hex-content-hash}.eml</c>. Deterministic for identical
/// content (no clock dependency), so the same email always maps to the same file.
/// </summary>
public sealed class DeterministicEmailFileNameStrategy : IEmailFileNameStrategy
{
    /// <inheritdoc />
    public string GetFileName(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Stable hash over the addressable content. Avoids string.GetHashCode (randomized per process).
        var payload = $"{message.Audience}\n{message.To}\n{message.Subject}\n{message.HtmlBody}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        var id = Convert.ToHexString(hash, 0, 4).ToLowerInvariant();

        return $"{message.Audience}-{id}.eml".ToLowerInvariant();
    }
}
