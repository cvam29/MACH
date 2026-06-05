namespace Mach.Infrastructure.Email;

/// <summary>Selects which <see cref="Application.Ports.IEmailSender"/> implementation is wired up.</summary>
public enum EmailProvider
{
    /// <summary>Writes emails as <c>.eml</c> files to a local folder for offline dev runs.</summary>
    DevSink,

    /// <summary>Sends via Azure Communication Services Email.</summary>
    Acs,
}

/// <summary>
/// Configuration for the email adapter, bound from the <c>Email:</c> section.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>The configuration section name these options bind to.</summary>
    public const string SectionName = "Email";

    /// <summary>Which sender implementation to use. Defaults to <see cref="EmailProvider.DevSink"/>.</summary>
    public EmailProvider Provider { get; set; } = EmailProvider.DevSink;

    /// <summary>
    /// ACS connection string. When null/empty and <see cref="Provider"/> is
    /// <see cref="EmailProvider.Acs"/>, managed identity is used against <see cref="AcsEndpoint"/>.
    /// </summary>
    public string? AcsConnectionString { get; set; }

    /// <summary>
    /// ACS resource endpoint (e.g. <c>https://my-acs.communication.azure.com</c>).
    /// Required for managed-identity auth when <see cref="AcsConnectionString"/> is not set.
    /// </summary>
    public string? AcsEndpoint { get; set; }

    /// <summary>The verified sender (From) address registered with the ACS resource.</summary>
    public string? FromAddress { get; set; }

    /// <summary>Target folder for the dev sink. Defaults to <c>./mail</c>.</summary>
    public string SinkDirectory { get; set; } = "./mail";
}
