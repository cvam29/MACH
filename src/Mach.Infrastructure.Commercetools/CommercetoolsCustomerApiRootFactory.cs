using commercetools.Base.Client;
using commercetools.Base.Serialization;

using commercetools.Sdk.Api.Client;

using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// Builds <see cref="ProjectApiRoot"/> instances whose underlying <see cref="IClient"/> authenticates
/// with a <em>caller-supplied</em> customer (or anonymous-session) access token rather than the
/// service client-credentials token. The HTTP transport and (de)serialization still flow entirely
/// through the SDK: we assemble an <see cref="IClient"/> with <see cref="ClientBuilder"/>, swapping in
/// a <see cref="CustomerAccessTokenProvider"/> and reusing the SDK's own
/// <see cref="ISerializerService"/> plus the resilience-enabled <see cref="HttpClient"/>.
/// </summary>
internal sealed class CommercetoolsCustomerApiRootFactory
{
    private readonly CommercetoolsOptions _options;
    private readonly ISerializerService _serializerService;
    private readonly IHttpClientFactory _httpClientFactory;

    public CommercetoolsCustomerApiRootFactory(
        IOptions<CommercetoolsOptions> options,
        ISerializerService serializerService,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _serializerService = serializerService;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Builds a customer-scoped <see cref="ProjectApiRoot"/> bound to <paramref name="accessToken"/>.
    /// The returned root issues requests via the SDK request DSL (e.g. <c>.Me().Get()</c>) with the
    /// supplied bearer token applied by the SDK's authorization middleware.
    /// </summary>
    public ProjectApiRoot Create(string accessToken)
    {
        var configuration = new ClientConfiguration
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            Scope = _options.ScopeList,
            ProjectKey = _options.ProjectKey,
            ApiBaseAddress = EnsureTrailingSlash(_options.ApiUrl),
            AuthorizationBaseAddress = EnsureTrailingSlash(_options.AuthUrl),
        };

        var tokenProvider = new CustomerAccessTokenProvider(accessToken)
        {
            ClientConfiguration = configuration,
        };

        // Reuse the same resilience-enabled HttpClient the SDK service path uses, so transient-fault
        // handling is identical for the customer-scoped calls.
        var httpClient = _httpClientFactory.CreateClient(
            CommercetoolsServiceCollectionExtensions.CustomerApiHttpClientName);

        var client = new ClientBuilder
        {
            ClientName = CommercetoolsServiceCollectionExtensions.CustomerApiHttpClientName,
            ClientConfiguration = configuration,
            SerializerService = _serializerService,
            TokenProvider = tokenProvider,
            HttpClient = httpClient,
        }.Build();

        return new ProjectApiRoot(client, _options.ProjectKey);
    }

    private static string EnsureTrailingSlash(string url) =>
        string.IsNullOrEmpty(url) || url.EndsWith('/') ? url : url + "/";
}
