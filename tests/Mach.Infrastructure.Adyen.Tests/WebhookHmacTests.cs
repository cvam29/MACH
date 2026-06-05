using Mach.Domain;
using Mach.Domain.ValueObjects;
using Mach.Infrastructure.Adyen;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using HmacValidator = global::Adyen.Util.HmacValidator;

namespace Mach.Infrastructure.Adyen.Tests;

/// <summary>
/// HMAC verification tests against the real Adyen <see cref="HmacValidator"/>. This is the most
/// important, fully deterministic part of the adapter: a valid signature must verify, a tampered
/// one must not.
/// </summary>
public sealed class WebhookHmacTests
{
    // Sample Adyen test HMAC key (hex), as used in Adyen's own documentation examples.
    private const string HmacKey = "44782DEF547AAA06C910C43932B1EB0C71FC68D9D0C057550C48EC2ACF6BA056";

    private static AdyenPaymentGateway BuildGateway(string hmacKey = HmacKey)
    {
        var options = Options.Create(new AdyenOptions
        {
            ApiKey = "test-api-key",
            MerchantAccount = "TestMerchant",
            HmacKey = hmacKey,
            Environment = "Test",
            ReturnUrl = "https://shop.example/return",
        });

        return new AdyenPaymentGateway(
            new StubCheckoutApi(),
            new HmacValidator(),
            options,
            NullLogger<AdyenPaymentGateway>.Instance);
    }

    /// <summary>Builds a notification whose embedded hmacSignature is computed with the given key.</summary>
    private static string BuildSignedNotification(string hmacKey, decimal majorAmount = 12.34m)
    {
        var minor = (long)(majorAmount * 100m);

        // Build the item first (without a signature) so we can compute the real HMAC over it.
        var validator = new HmacValidator();
        var item = new WebhookModels.NotificationRequestItem
        {
            Amount = new WebhookModels.Amount { Currency = "EUR", Value = minor },
            EventCode = "AUTHORISATION",
            EventDate = "2026-06-05T10:15:00+02:00",
            MerchantAccountCode = "TestMerchant",
            MerchantReference = "cart-123",
            PspReference = "PSP-987",
            Success = true,
        };

        var signature = validator.CalculateHmac(item, hmacKey);
        return NotificationJson(minor, signature, success: "true");
    }

    private static string NotificationJson(long minor, string? hmacSignature, string success)
    {
        var additional = hmacSignature is null
            ? string.Empty
            : $", \"additionalData\": {{ \"hmacSignature\": \"{hmacSignature}\" }}";

        return $$"""
        {
          "live": "false",
          "notificationItems": [
            {
              "NotificationRequestItem": {
                "amount": { "currency": "EUR", "value": {{minor}} },
                "eventCode": "AUTHORISATION",
                "eventDate": "2026-06-05T10:15:00+02:00",
                "merchantAccountCode": "TestMerchant",
                "merchantReference": "cart-123",
                "pspReference": "PSP-987",
                "success": "{{success}}"{{additional}}
              }
            }
          ]
        }
        """;
    }

    [Fact]
    public void VerifyWebhookSignature_with_valid_embedded_hmac_returns_true()
    {
        var gateway = BuildGateway();
        var body = BuildSignedNotification(HmacKey);

        gateway.VerifyWebhookSignature(body, hmacSignature: string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void VerifyWebhookSignature_with_tampered_payload_returns_false()
    {
        var gateway = BuildGateway();

        // Sign for one amount, then ship a different amount -> embedded signature no longer matches.
        var validator = new HmacValidator();
        var signedItem = new WebhookModels.NotificationRequestItem
        {
            Amount = new WebhookModels.Amount { Currency = "EUR", Value = 1234 },
            EventCode = "AUTHORISATION",
            EventDate = "2026-06-05T10:15:00+02:00",
            MerchantAccountCode = "TestMerchant",
            MerchantReference = "cart-123",
            PspReference = "PSP-987",
            Success = true,
        };
        var signature = validator.CalculateHmac(signedItem, HmacKey);

        // Tamper: value changed from 1234 to 9999 while keeping the old signature.
        var tamperedBody = NotificationJson(minor: 9999, hmacSignature: signature, success: "true");

        gateway.VerifyWebhookSignature(tamperedBody, hmacSignature: string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void VerifyWebhookSignature_with_wrong_key_returns_false()
    {
        // Signature computed with the real key, but the gateway is configured with a different key.
        var body = BuildSignedNotification(HmacKey);
        var gateway = BuildGateway(hmacKey: "00000000000000000000000000000000000000000000000000000000000000FF");

        gateway.VerifyWebhookSignature(body, hmacSignature: string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void VerifyWebhookSignature_with_matching_supplied_signature_returns_true()
    {
        var gateway = BuildGateway();

        var validator = new HmacValidator();
        var item = new WebhookModels.NotificationRequestItem
        {
            Amount = new WebhookModels.Amount { Currency = "EUR", Value = 1234 },
            EventCode = "AUTHORISATION",
            EventDate = "2026-06-05T10:15:00+02:00",
            MerchantAccountCode = "TestMerchant",
            MerchantReference = "cart-123",
            PspReference = "PSP-987",
            Success = true,
        };
        var signature = validator.CalculateHmac(item, HmacKey);
        var body = NotificationJson(minor: 1234, hmacSignature: signature, success: "true");

        gateway.VerifyWebhookSignature(body, hmacSignature: signature).ShouldBeTrue();
    }

    [Fact]
    public void VerifyWebhookSignature_with_empty_body_returns_false()
    {
        BuildGateway().VerifyWebhookSignature(string.Empty, "sig").ShouldBeFalse();
    }

    /// <summary>Stub used when the test does not exercise session creation.</summary>
    private sealed class StubCheckoutApi : IAdyenCheckoutApi
    {
        public Task<CheckoutModels.CreateCheckoutSessionResponse> CreateSessionAsync(
            CheckoutModels.CreateCheckoutSessionRequest request, CancellationToken ct)
            => throw new NotSupportedException("Session creation is not exercised in this test.");
    }
}
