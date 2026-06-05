using Mach.Application.Dtos;
using Mach.Domain;

namespace Mach.Application.Ports;

/// <summary>
/// Port over transactional email (Azure Communication Services, or a local dev sink).
/// Implemented by <c>Mach.Infrastructure.Email</c>.
/// </summary>
public interface IEmailSender
{
    Task<Result> SendAsync(EmailMessage message, CancellationToken ct);
}
