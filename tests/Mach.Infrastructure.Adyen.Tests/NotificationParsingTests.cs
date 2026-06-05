using Mach.Domain;
using Mach.Infrastructure.Adyen;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using HmacValidator = global::Adyen.Util.HmacValidator;

namespace Mach.Infrastructure.Adyen.Tests;

/// <summary>Tests that a sample webhook JSON parses into our normalized notification model.</summary>
public sealed class NotificationParsingTests
{
    private static AdyenPaymentGateway BuildGateway()
    {
        var options = Options.Create(new AdyenOptions
        {
            ApiKey = "test-api-key",
            MerchantAccount = "TestMerchant",
            HmacKey = "44782DEF547AAA06C910C43932B1EB0C71FC68D9D0C057550C48EC2ACF6BA056",
            Environment = "Test",
        });

        return new AdyenPaymentGateway(
            new StubPaymentsService(),
            new HmacValidator(),
            options,
            NullLogger<AdyenPaymentGateway>.Instance);
    }

    [Fact]
    public void ParseNotification_maps_successful_authorisation()
    {
        const string body = """
        {
          "live": "false",
          "notificationItems": [
            {
              "NotificationRequestItem": {
                "amount": { "currency": "EUR", "value": 1599 },
                "eventCode": "AUTHORISATION",
                "eventDate": "2026-06-05T10:15:00+02:00",
                "merchantAccountCode": "TestMerchant",
                "merchantReference": "cart-abc",
                "pspReference": "PSP-12345",
                "success": "true"
              }
            }
          ]
        }
        """;

        var result = BuildGateway().ParseNotification(body);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);

        var n = result.Value[0];
        n.PspReference.ShouldBe("PSP-12345");
        n.MerchantReference.ShouldBe("cart-abc");
        n.EventCode.ShouldBe("AUTHORISATION");
        n.Success.ShouldBeTrue();
        n.Status.ShouldBe(PaymentStatus.Authorized);
        n.Amount.Currency.ShouldBe("EUR");
        n.Amount.Amount.ShouldBe(15.99m);
        n.EventDate.ShouldBe(DateTimeOffset.Parse("2026-06-05T10:15:00+02:00"));
    }

    [Fact]
    public void ParseNotification_maps_refused_authorisation_to_refused_status()
    {
        const string body = """
        {
          "live": "false",
          "notificationItems": [
            {
              "NotificationRequestItem": {
                "amount": { "currency": "USD", "value": 500 },
                "eventCode": "AUTHORISATION",
                "eventDate": "2026-06-05T11:00:00Z",
                "merchantAccountCode": "TestMerchant",
                "merchantReference": "cart-xyz",
                "pspReference": "PSP-99",
                "success": "false",
                "reason": "Refused"
              }
            }
          ]
        }
        """;

        var n = BuildGateway().ParseNotification(body).Value.Single();

        n.Success.ShouldBeFalse();
        n.Status.ShouldBe(PaymentStatus.Refused);
        n.Amount.Amount.ShouldBe(5.00m);
        n.Amount.Currency.ShouldBe("USD");
    }

    [Fact]
    public void ParseNotification_maps_multiple_items()
    {
        const string body = """
        {
          "live": "false",
          "notificationItems": [
            { "NotificationRequestItem": { "amount": { "currency": "EUR", "value": 100 }, "eventCode": "AUTHORISATION", "eventDate": "2026-06-05T11:00:00Z", "merchantReference": "c1", "pspReference": "p1", "success": "true" } },
            { "NotificationRequestItem": { "amount": { "currency": "EUR", "value": 200 }, "eventCode": "CAPTURE", "eventDate": "2026-06-05T11:05:00Z", "merchantReference": "c1", "pspReference": "p1", "success": "true" } }
          ]
        }
        """;

        var items = BuildGateway().ParseNotification(body).Value;

        items.Count.ShouldBe(2);
        items[0].EventCode.ShouldBe("AUTHORISATION");
        items[0].Status.ShouldBe(PaymentStatus.Authorized);
        items[1].EventCode.ShouldBe("CAPTURE");
        items[1].Status.ShouldBe(PaymentStatus.Captured);
    }

    [Fact]
    public void ParseNotification_with_empty_body_fails_validation()
    {
        var result = BuildGateway().ParseNotification("   ");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
    }

    [Fact]
    public void ParseNotification_with_malformed_json_fails_validation()
    {
        var result = BuildGateway().ParseNotification("{ not json");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
    }
}
