using Mach.Domain.Auth;
using Mach.Infrastructure.Commercetools;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Shouldly;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Mach.Infrastructure.Commercetools.Tests;

public sealed class CommercetoolsTokenClientTests : IDisposable
{
    // A fixed clock so token-expiry timestamps are asserted exactly, not "greater than now".
    private static readonly DateTimeOffset Now = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly WireMockServer _auth;
    private readonly FakeTimeProvider _time = new(Now);
    private readonly CommercetoolsTokenClient _client;

    public CommercetoolsTokenClientTests()
    {
        _auth = WireMockServer.Start();
        var options = Options.Create(new CommercetoolsOptions
        {
            ProjectKey = "demo",
            ClientId = "client",
            ClientSecret = "secret",
            ScopeList = "manage_project:demo",
            ApiUrl = "http://localhost/api",
            AuthUrl = _auth.Url!,
        });
        _client = new CommercetoolsTokenClient(new HttpClient(), options, _time);
    }

    [Fact]
    public async Task PasswordAsync_returns_session_with_access_and_refresh_tokens()
    {
        _auth.Given(Request.Create().WithPath("/oauth/demo/customers/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {"access_token":"acc-1","refresh_token":"ref-1","token_type":"Bearer","expires_in":172800,"scope":"manage_project:demo customer_id:cust-1"}
                    """));

        var result = await _client.PasswordAsync(new Credentials("a@b.com", "pw"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("acc-1");
        result.Value.RefreshToken.ShouldBe("ref-1");
        // expires_in 172800s from the fixed clock — deterministic thanks to the injected TimeProvider.
        result.Value.ExpiresAt.ShouldBe(Now.AddSeconds(172800));
        result.Value.AnonymousId.ShouldBeNull();
    }

    [Fact]
    public async Task PasswordAsync_sends_password_grant_and_basic_auth()
    {
        _auth.Given(Request.Create().WithPath("/oauth/demo/customers/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""{"access_token":"acc","expires_in":3600}"""));

        await _client.PasswordAsync(new Credentials("user@x.com", "pw"), CancellationToken.None);

        var request = _auth.LogEntries.Single().RequestMessage!;
        request.Headers!["Authorization"]!.ToString().ShouldStartWith("Basic ");
        var body = request.Body ?? string.Empty;
        body.ShouldContain("grant_type=password");
        body.ShouldContain("username=user%40x.com");
        body.ShouldContain("password=pw");
    }

    [Fact]
    public async Task AnonymousAsync_extracts_anonymous_id_from_scope()
    {
        _auth.Given(Request.Create().WithPath("/oauth/demo/anonymous/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""
                    {"access_token":"anon-acc","refresh_token":"anon-ref","expires_in":3600,"scope":"manage_project:demo anonymous_id:guest-42"}
                    """));

        var result = await _client.AnonymousAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AnonymousId.ShouldBe("guest-42");
        result.Value.IsAnonymous.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_carries_refresh_token_when_not_reissued()
    {
        _auth.Given(Request.Create().WithPath("/oauth/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""{"access_token":"acc-2","expires_in":3600}"""));

        var result = await _client.RefreshAsync("original-refresh", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("acc-2");
        result.Value.RefreshToken.ShouldBe("original-refresh");

        var body = _auth.LogEntries.Single().RequestMessage!.Body ?? string.Empty;
        body.ShouldContain("grant_type=refresh_token");
        body.ShouldContain("refresh_token=original-refresh");
    }

    [Fact]
    public async Task PasswordAsync_translates_400_to_validation_failure()
    {
        _auth.Given(Request.Create().WithPath("/oauth/demo/customers/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(400)
                .WithBody("""{"error":"invalid_customer_account_credentials"}"""));

        var result = await _client.PasswordAsync(new Credentials("a@b.com", "wrong"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
    }

    [Fact]
    public async Task PasswordAsync_translates_500_to_unexpected_failure()
    {
        _auth.Given(Request.Create().WithPath("/oauth/demo/customers/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503).WithBody("down"));

        var result = await _client.PasswordAsync(new Credentials("a@b.com", "pw"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("unexpected");
    }

    public void Dispose() => _auth.Dispose();
}
