using Mach.Domain;

namespace Mach.Application.Dtos;

/// <summary>A generic CMS content entry (Contentstack), payload kept as a property bag.</summary>
public sealed record ContentEntryDto(
    string ContentType,
    string Slug,
    string Title,
    IReadOnlyDictionary<string, object?> Fields);

/// <summary>A navigation node (with children) sourced from the CMS.</summary>
public sealed record NavigationNodeDto(
    string Label,
    string Url,
    IReadOnlyList<NavigationNodeDto> Children);

/// <summary>
/// An email template authored in the CMS, keyed by audience. Body may contain
/// <c>{{order}}</c> / <c>{{delivery}}</c> style tokens resolved at send time.
/// </summary>
public sealed record EmailTemplateDto(
    NotificationAudience Audience,
    string Subject,
    string HtmlBody);
