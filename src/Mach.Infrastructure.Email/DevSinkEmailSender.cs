using System.Text;
using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Email;

/// <summary>
/// Local dev <see cref="IEmailSender"/> that writes each email as an RFC822-ish <c>.eml</c> file
/// into a configurable folder. Lets local runs show all sent emails offline with no external service.
/// </summary>
public sealed class DevSinkEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly IEmailFileNameStrategy _fileNameStrategy;
    private readonly ILogger<DevSinkEmailSender> _logger;

    public DevSinkEmailSender(
        IOptions<EmailOptions> options,
        IEmailFileNameStrategy fileNameStrategy,
        ILogger<DevSinkEmailSender>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _fileNameStrategy = fileNameStrategy ?? throw new ArgumentNullException(nameof(fileNameStrategy));
        _logger = logger ?? NullLogger<DevSinkEmailSender>.Instance;
    }

    /// <inheritdoc />
    public async Task<Result> SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var directory = string.IsNullOrWhiteSpace(_options.SinkDirectory) ? "./mail" : _options.SinkDirectory;
            Directory.CreateDirectory(directory);

            var fileName = _fileNameStrategy.GetFileName(message);
            var path = Path.Combine(directory, fileName);

            var content = Render(message, _options.FromAddress);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), ct).ConfigureAwait(false);

            _logger.LogInformation(
                "DevSink wrote email for audience {Audience} to {Path}", message.Audience, path);

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(Error.Unexpected($"Failed to write dev-sink email: {ex.Message}"));
        }
    }

    /// <summary>Renders a minimal, deterministic RFC822-style message (no Date header, for testability).</summary>
    private static string Render(EmailMessage message, string? from)
    {
        var sb = new StringBuilder();
        sb.Append("From: ").Append(from ?? "no-reply@localhost").Append('\n');
        sb.Append("To: ").Append(message.To).Append('\n');
        sb.Append("Subject: ").Append(message.Subject).Append('\n');
        sb.Append("X-Mach-Audience: ").Append(message.Audience).Append('\n');
        sb.Append("MIME-Version: 1.0\n");
        sb.Append("Content-Type: text/html; charset=utf-8\n");
        sb.Append('\n');
        sb.Append(message.HtmlBody);
        sb.Append('\n');
        return sb.ToString();
    }
}
