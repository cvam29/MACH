using System.Text.Json;

using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using HmacValidator = Adyen.Util.HmacValidator;
using IPaymentsService = Adyen.Checkout.Services.IPaymentsService;
using NotificationRequest = Adyen.Webhooks.Models.NotificationRequest;

namespace Mach.Infrastructure.Adyen;

/// <summary>
/// Adyen-backed <see cref="IPaymentGateway"/>: creates Checkout payment sessions through the Adyen
/// .NET SDK's typed checkout <see cref="IPaymentsService"/> (its <c>Sessions</c> API), verifies
/// webhook HMAC signatures using Adyen's <see cref="HmacValidator"/>, and parses notifications into
/// our normalized <see cref="PaymentNotificationDto"/> model.
/// </summary>
internal sealed class AdyenPaymentGateway : IPaymentGateway
{
    private readonly IPaymentsService _paymentsService;
    private readonly HmacValidator _hmacValidator;
    private readonly AdyenOptions _options;
    private readonly ILogger<AdyenPaymentGateway> _logger;

    public AdyenPaymentGateway(
        IPaymentsService paymentsService,
        HmacValidator hmacValidator,
        IOptions<AdyenOptions> options,
        ILogger<AdyenPaymentGateway> logger)
    {
        _paymentsService = paymentsService;
        _hmacValidator = hmacValidator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<PaymentSessionDto>> CreatePaymentSessionAsync(
        CartId cartId, Money amount, CancellationToken ct)
    {
        try
        {
            var request = AdyenMapping.BuildSessionRequest(_options, cartId, amount);

            // Drive the call through the Adyen SDK's typed checkout service (Sessions API).
            var apiResponse = await _paymentsService
                .SessionsAsync(request, requestOptions: null, cancellationToken: ct)
                .ConfigureAwait(false);

            if (!apiResponse.IsCreated)
            {
                return Error.Unexpected("Adyen did not create a checkout session (unexpected response status).");
            }

            var response = apiResponse.Created();
            return AdyenMapping.MapSessionResponse(response, cartId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to create Adyen payment session for cart {CartId}.", cartId.Value);
            return Error.Unexpected($"Failed to create Adyen payment session: {ex.Message}");
        }
    }

    public bool VerifyWebhookSignature(string rawBody, string hmacSignature)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        try
        {
            var request = JsonSerializer.Deserialize<NotificationRequest>(rawBody, AdyenJson.Webhook);
            var item = request?.NotificationItemContainers?.FirstOrDefault()?.NotificationItem;
            if (item is null)
            {
                return false;
            }

            // Verify against the signature embedded in the notification (additionalData.hmacSignature),
            // and additionally against the explicitly supplied signature when one is provided.
            var expected = _hmacValidator.CalculateHmac(item, _options.HmacKey);

            var embeddedValid = _hmacValidator.IsValidHmac(item, _options.HmacKey);
            var suppliedValid = !string.IsNullOrEmpty(hmacSignature)
                && CryptographicEquals(expected, hmacSignature);

            return string.IsNullOrEmpty(hmacSignature) ? embeddedValid : (embeddedValid && suppliedValid);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to verify Adyen webhook HMAC signature.");
            return false;
        }
    }

    public Result<IReadOnlyList<PaymentNotificationDto>> ParseNotification(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return Error.Validation("Webhook body was empty.");
        }

        try
        {
            var request = JsonSerializer.Deserialize<NotificationRequest>(rawBody, AdyenJson.Webhook);
            var containers = request?.NotificationItemContainers;
            if (containers is null || containers.Count == 0)
            {
                return Error.Validation("Webhook contained no notification items.");
            }

            var result = new List<PaymentNotificationDto>(containers.Count);
            foreach (var container in containers)
            {
                if (container.NotificationItem is { } item)
                {
                    result.Add(AdyenMapping.MapNotificationItem(item));
                }
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Adyen webhook notification body.");
            return Error.Validation($"Malformed Adyen notification: {ex.Message}");
        }
    }

    /// <summary>Constant-time string comparison to avoid timing leaks on signature checks.</summary>
    private static bool CryptographicEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
