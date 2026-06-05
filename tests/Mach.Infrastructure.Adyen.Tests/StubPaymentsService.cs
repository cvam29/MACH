using Adyen.Checkout.Models;
using Adyen.Checkout.Services;
using Adyen.Core.Client;

namespace Mach.Infrastructure.Adyen.Tests;

/// <summary>
/// A no-op <see cref="IPaymentsService"/> stub for tests that exercise HMAC verification or
/// notification parsing and never create a session. Every API call throws to make accidental use
/// obvious.
/// </summary>
internal sealed class StubPaymentsService : IPaymentsService
{
    public PaymentsServiceEvents Events => new();

    public HttpClient HttpClient => new();

    public Task<ICardDetailsApiResponse> CardDetailsAsync(
        CardDetailsRequest cardDetailsRequest,
        RequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised in this test.");

    public Task<IGetResultOfPaymentSessionApiResponse> GetResultOfPaymentSessionAsync(
        string sessionId,
        string sessionResult,
        RequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised in this test.");

    public Task<IPaymentMethodsApiResponse> PaymentMethodsAsync(
        PaymentMethodsRequest paymentMethodsRequest,
        RequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised in this test.");

    public Task<IPaymentsApiResponse> PaymentsAsync(
        PaymentRequest paymentRequest,
        RequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised in this test.");

    public Task<IPaymentsDetailsApiResponse> PaymentsDetailsAsync(
        PaymentDetailsRequest paymentDetailsRequest,
        RequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised in this test.");

    public Task<ISessionsApiResponse> SessionsAsync(
        CreateCheckoutSessionRequest createCheckoutSessionRequest,
        RequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not exercised in this test.");
}
