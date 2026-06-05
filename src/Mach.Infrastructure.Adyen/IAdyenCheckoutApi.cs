using CreateCheckoutSessionRequest = Adyen.Checkout.Models.CreateCheckoutSessionRequest;
using CreateCheckoutSessionResponse = Adyen.Checkout.Models.CreateCheckoutSessionResponse;

namespace Mach.Infrastructure.Adyen;

/// <summary>
/// Thin seam over the Adyen Checkout <c>/sessions</c> HTTP call. Isolating the network call
/// behind this interface keeps <see cref="AdyenPaymentGateway"/> request-building and
/// response-mapping logic unit-testable, and lets integration tests point a real implementation
/// at a WireMock endpoint.
/// </summary>
internal interface IAdyenCheckoutApi
{
    Task<CreateCheckoutSessionResponse> CreateSessionAsync(
        CreateCheckoutSessionRequest request, CancellationToken ct);
}
