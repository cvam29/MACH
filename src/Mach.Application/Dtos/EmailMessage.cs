using Mach.Domain;

namespace Mach.Application.Dtos;

/// <summary>A transactional email to be delivered to a single audience recipient.</summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    NotificationAudience Audience);
