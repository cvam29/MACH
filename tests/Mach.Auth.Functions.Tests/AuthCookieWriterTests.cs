using Mach.Auth.Functions;
using Mach.Domain.Auth;
using Mach.Domain.ValueObjects;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Shouldly;

namespace Mach.Auth.Functions.Tests;

public sealed class AuthCookieWriterTests
{
    // A fixed clock so cookie-expiry timestamps are asserted exactly.
    private static readonly DateTimeOffset Now = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);

    private static AuthCookieWriter CreateWriter(AuthCookieOptions? options = null)
        => new(Options.Create(options ?? new AuthCookieOptions()), new FakeTimeProvider(Now));

    private static CustomerSession Session(
        string access = "access-token",
        string refresh = "refresh-token",
        DateTimeOffset? expiresAt = null)
        => new(access, refresh, expiresAt ?? DateTimeOffset.UtcNow.AddHours(2),
            new CustomerId("cust-1"), null);

    [Fact]
    public void SetSessionCookies_emits_httponly_secure_lax_path_root()
    {
        var writer = CreateWriter();
        var ctx = new DefaultHttpContext();

        writer.SetSessionCookies(ctx.Response, Session());

        var setCookies = ctx.Response.Headers.SetCookie.Select(h => h ?? string.Empty).ToArray();
        setCookies.Length.ShouldBe(2);

        var access = setCookies.Single(h => h.StartsWith("mach_at="));
        access.ShouldContain("httponly", Case.Insensitive);
        access.ShouldContain("secure", Case.Insensitive);
        access.ShouldContain("samesite=lax", Case.Insensitive);
        access.ShouldContain("path=/", Case.Insensitive);

        var refresh = setCookies.Single(h => h.StartsWith("mach_rt="));
        refresh.ShouldContain("httponly", Case.Insensitive);
        refresh.ShouldContain("secure", Case.Insensitive);
        refresh.ShouldContain("samesite=lax", Case.Insensitive);
        refresh.ShouldContain("path=/", Case.Insensitive);
    }

    [Fact]
    public void Access_cookie_expiry_derives_from_session_ExpiresAt()
    {
        var writer = CreateWriter();
        var expires = DateTimeOffset.UtcNow.AddMinutes(45);

        var opts = writer.BuildAccessCookieOptions(Session(expiresAt: expires));

        opts.Expires.ShouldNotBeNull();
        opts.Expires!.Value.ShouldBe(expires, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Refresh_cookie_uses_longer_window_than_access()
    {
        var writer = CreateWriter(new AuthCookieOptions
        {
            RefreshCookieLifetime = TimeSpan.FromDays(30),
        });

        var refreshOpts = writer.BuildRefreshCookieOptions();

        refreshOpts.Expires.ShouldNotBeNull();
        // Exact: fixed clock + 30-day lifetime (deterministic via the injected TimeProvider).
        refreshOpts.Expires!.Value.ShouldBe(Now.AddDays(30));
    }

    [Fact]
    public void ClearSessionCookies_expires_both_cookies_in_the_past()
    {
        var writer = CreateWriter();
        var ctx = new DefaultHttpContext();

        writer.ClearSessionCookies(ctx.Response);

        var setCookies = ctx.Response.Headers.SetCookie.Select(h => h ?? string.Empty).ToArray();
        setCookies.Length.ShouldBe(2);
        foreach (var header in setCookies)
        {
            header.ShouldContain("expires=", Case.Insensitive);
        }
    }

    [Fact]
    public void Anonymous_session_without_refresh_token_omits_refresh_cookie()
    {
        var writer = CreateWriter();
        var ctx = new DefaultHttpContext();
        var anon = new CustomerSession(
            "anon-access", string.Empty, DateTimeOffset.UtcNow.AddHours(1), null, "anon-1");

        writer.SetSessionCookies(ctx.Response, anon);

        var setCookies = ctx.Response.Headers.SetCookie.Select(h => h ?? string.Empty).ToArray();
        setCookies.Length.ShouldBe(1);
        setCookies.Single().ShouldStartWith("mach_at=");
    }

    [Fact]
    public void ReadAccessToken_and_ReadRefreshToken_round_trip()
    {
        var writer = CreateWriter();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "mach_at=the-access; mach_rt=the-refresh";

        writer.ReadAccessToken(ctx.Request).ShouldBe("the-access");
        writer.ReadRefreshToken(ctx.Request).ShouldBe("the-refresh");
    }

    [Fact]
    public void ReadAccessToken_returns_null_when_cookie_absent()
    {
        var writer = CreateWriter();
        var ctx = new DefaultHttpContext();

        writer.ReadAccessToken(ctx.Request).ShouldBeNull();
    }
}
