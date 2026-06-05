using Mach.Application.Ports;
using Mach.Domain.Auth;
using Mach.Infrastructure.Commercetools;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Mach.Infrastructure.Commercetools.Tests;

/// <summary>
/// Exercises the public <see cref="ICustomerAuth"/> surface through the DI registration, against a
/// WireMock auth + API server, to cover the password, anonymous and refresh token flows and /me.
/// </summary>
public sealed class CommercetoolsCustomerAuthTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly ServiceProvider _provider;
    private readonly ICustomerAuth _auth;

    public CommercetoolsCustomerAuthTests()
    {
        _server = WireMockServer.Start();

        // Service-client credentials token (used by the SDK for /me).
        _server.Given(Request.Create().WithPath("/oauth/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""{"access_token":"svc","expires_in":3600,"scope":"manage_project:demo"}"""));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Commercetools:ProjectKey"] = "demo",
                ["Commercetools:ClientId"] = "id",
                ["Commercetools:ClientSecret"] = "secret",
                ["Commercetools:ScopeList"] = "manage_project:demo",
                ["Commercetools:ApiUrl"] = _server.Url!,
                ["Commercetools:AuthUrl"] = _server.Url!,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCommercetools(config);
        _provider = services.BuildServiceProvider();
        _auth = _provider.GetRequiredService<ICustomerAuth>();
    }

    [Fact]
    public async Task AnonymousSessionAsync_returns_anonymous_session()
    {
        _server.Given(Request.Create().WithPath("/oauth/demo/anonymous/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""{"access_token":"a","refresh_token":"r","expires_in":3600,"scope":"manage_project:demo anonymous_id:guest-1"}"""));

        var result = await _auth.AnonymousSessionAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsAnonymous.ShouldBeTrue();
        result.Value.AnonymousId.ShouldBe("guest-1");
    }

    [Fact]
    public async Task LoginAsync_attaches_customer_id_from_me()
    {
        _server.Given(Request.Create().WithPath("/oauth/demo/customers/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""{"access_token":"cust-token","refresh_token":"cust-ref","expires_in":3600,"scope":"manage_project:demo"}"""));

        _server.Given(Request.Create().WithPath("/demo/me").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "id": "cust-1", "version": 1,
                  "createdAt": "2024-01-01T00:00:00.000Z", "lastModifiedAt": "2024-01-01T00:00:00.000Z",
                  "email": "ada@x.com", "firstName": "Ada", "lastName": "Byte",
                  "addresses": [], "isEmailVerified": true, "authenticationMode": "Password",
                  "stores": []
                }
                """));

        var result = await _auth.LoginAsync(new Credentials("ada@x.com", "pw"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("cust-token");
        result.Value.CustomerId!.Value.Value.ShouldBe("cust-1");
    }

    [Fact]
    public async Task RefreshAsync_returns_new_access_token()
    {
        // Re-map /oauth/token at a higher priority so the refresh flow gets a distinct token from
        // the service client-credentials response registered in the constructor.
        _server.Given(Request.Create().WithPath("/oauth/token").UsingPost())
            .AtPriority(-1)
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""{"access_token":"refreshed","expires_in":3600}"""));

        var result = await _auth.RefreshAsync("the-refresh-token", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("refreshed");
        result.Value.RefreshToken.ShouldBe("the-refresh-token");
    }

    [Fact]
    public async Task GetMeAsync_maps_customer_profile()
    {
        _server.Given(Request.Create().WithPath("/demo/me").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "id": "cust-2", "version": 3,
                  "createdAt": "2024-01-01T00:00:00.000Z", "lastModifiedAt": "2024-01-01T00:00:00.000Z",
                  "email": "grace@x.com", "firstName": "Grace", "lastName": "Hopper",
                  "addresses": [{ "country": "US", "city": "NYC", "streetName": "Wall", "postalCode": "10005" }],
                  "isEmailVerified": true, "authenticationMode": "Password", "stores": []
                }
                """));

        var result = await _auth.GetMeAsync("any-access-token", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.Value.ShouldBe("cust-2");
        result.Value.Email.ShouldBe("grace@x.com");
        result.Value.Addresses.Count.ShouldBe(1);
        result.Value.Addresses[0].City.ShouldBe("NYC");
    }

    public void Dispose()
    {
        _provider.Dispose();
        _server.Dispose();
    }
}
