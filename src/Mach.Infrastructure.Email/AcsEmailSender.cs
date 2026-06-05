using Azure;
using Azure.Communication.Email;
using Mach.Application.Ports;
using Mach.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using AcsEmailMessage = Azure.Communication.Email.EmailMessage;
using EmailMessage = Mach.Application.Dtos.EmailMessage;

namespace Mach.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> backed by Azure Communication Services Email. The sender (From)
/// address comes from <see cref="EmailOptions.FromAddress"/>.
/// </summary>
public sealed class AcsEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly EmailOptions _options;
    private readonly ILogger<AcsEmailSender> _logger;

    public AcsEmailSender(
        EmailClient client,
        IOptions<EmailOptions> options,
        ILogger<AcsEmailSender>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? NullLogger<AcsEmailSender>.Instance;
    }

    /// <inheritdoc />
    public async Task<Result> SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            return Result.Failure(Error.Validation("Email:FromAddress is not configured for the ACS provider."));
        }

        var acsMessage = BuildMessage(message, _options.FromAddress);

        try
        {
            var operation = await _client
                .SendAsync(WaitUntil.Started, acsMessage, ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "ACS accepted email for audience {Audience}, operation {OperationId}",
                message.Audience, operation.Id);

            return Result.Success();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "ACS rejected email for audience {Audience}", message.Audience);
            return Result.Failure(Error.Unexpected($"ACS send failed ({ex.ErrorCode}): {ex.Message}"));
        }
    }

    /// <summary>Maps the domain <see cref="EmailMessage"/> onto an ACS message. Pure; used by tests.</summary>
    internal static AcsEmailMessage BuildMessage(EmailMessage message, string fromAddress)
    {
        var content = new EmailContent(message.Subject)
        {
            Html = message.HtmlBody,
        };

        var recipients = new EmailRecipients(new[] { new EmailAddress(message.To) });

        return new AcsEmailMessage(fromAddress, recipients, content);
    }
}
