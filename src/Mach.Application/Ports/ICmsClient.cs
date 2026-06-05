using Mach.Application.Dtos;
using Mach.Domain;

namespace Mach.Application.Ports;

/// <summary>
/// Port over the headless CMS (Contentstack): content entries, navigation and email-template copy.
/// Implemented by <c>Mach.Infrastructure.Contentstack</c>.
/// </summary>
public interface ICmsClient
{
    Task<Result<ContentEntryDto>> GetEntryAsync(string contentType, string slug, CancellationToken ct);

    Task<Result<NavigationNodeDto>> GetNavigationAsync(CancellationToken ct);

    Task<Result<EmailTemplateDto>> GetEmailTemplateAsync(
        NotificationAudience audience, CancellationToken ct);
}
