using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Auth.Functions;
using Mach.Domain;
using Mach.Domain.Auth;
using Mach.Domain.ValueObjects;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Mach.Auth.Functions.Tests;

public sealed class AuthFunctionsTests
{
    private static AuthFunctions Create(ICustomerAuth auth)
        => new(auth, new AuthCookieWriter(Options.Create(new AuthCookieOptions()), TimeProvider.System),
            NullLogger<AuthFunctions>.Instance);

    private static HttpRequest JsonRequest(string body)
    {
        var ctx = new DefaultHttpContext();
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        ctx.Request.Body = new MemoryStream(bytes);
        ctx.Request.ContentLength = bytes.Length;
        ctx.Request.ContentType = "application/json";
        ctx.Request.Method = "POST";
        return ctx.Request;
    }

    private static int StatusOf(IResult result) => result switch
    {
        IStatusCodeHttpResult s => s.StatusCode ?? 0,
        _ => 0,
    };

    [Fact]
    public async Task Login_success_sets_cookies_and_returns_200()
    {
        var session = new CustomerSession(
            "at", "rt", DateTimeOffset.UtcNow.AddHours(1), new CustomerId("c1"), null);
        var fn = Create(new FakeAuth { LoginResult = Result.Success(session) });
        var request = JsonRequest("""{"email":"a@b.com","password":"pw"}""");

        var result = await fn.Login(request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status200OK);
        request.HttpContext.Response.Headers.SetCookie.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Login_missing_password_returns_400_without_calling_adapter()
    {
        var fake = new FakeAuth();
        var fn = Create(fake);
        var request = JsonRequest("""{"email":"a@b.com"}""");

        var result = await fn.Login(request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status400BadRequest);
        fake.LoginCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Login_adapter_validation_failure_maps_to_400()
    {
        var fn = Create(new FakeAuth
        {
            LoginResult = Result.Failure<CustomerSession>(Error.Validation("bad creds")),
        });
        var request = JsonRequest("""{"email":"a@b.com","password":"pw"}""");

        var result = await fn.Login(request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Register_success_returns_201_and_sets_cookies()
    {
        var session = new CustomerSession(
            "at", "rt", DateTimeOffset.UtcNow.AddHours(1), new CustomerId("c1"), null);
        var fn = Create(new FakeAuth { RegisterResult = Result.Success(session) });
        var request = JsonRequest(
            """{"email":"a@b.com","password":"pw","firstName":"A","lastName":"B"}""");

        var result = await fn.Register(request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status201Created);
        request.HttpContext.Response.Headers.SetCookie.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401()
    {
        var fn = Create(new FakeAuth());
        var ctx = new DefaultHttpContext();

        var result = await fn.Refresh(ctx.Request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Refresh_failure_clears_cookies_and_returns_401()
    {
        var fn = Create(new FakeAuth
        {
            RefreshResult = Result.Failure<CustomerSession>(Error.Validation("expired")),
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "mach_rt=stale";

        var result = await fn.Refresh(ctx.Request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status401Unauthorized);
        ctx.Response.Headers.SetCookie.Count.ShouldBe(2); // cleared
    }

    [Fact]
    public void Logout_clears_cookies_and_returns_204()
    {
        var fn = Create(new FakeAuth());
        var ctx = new DefaultHttpContext();

        var result = fn.Logout(ctx.Request);

        StatusOf(result).ShouldBe(StatusCodes.Status204NoContent);
        ctx.Response.Headers.SetCookie.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Me_without_cookies_returns_401()
    {
        var fn = Create(new FakeAuth());
        var ctx = new DefaultHttpContext();

        var result = await fn.Me(ctx.Request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Me_with_valid_access_cookie_returns_profile()
    {
        var customer = new CustomerDto(
            new CustomerId("c1"), "a@b.com", "A", "B", []);
        var fn = Create(new FakeAuth { MeResult = Result.Success(customer) });
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "mach_at=good";

        var result = await fn.Me(ctx.Request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Me_with_expired_access_refreshes_then_retries_and_resets_cookies()
    {
        var refreshed = new CustomerSession(
            "new-at", "new-rt", DateTimeOffset.UtcNow.AddHours(1), new CustomerId("c1"), null);
        var customer = new CustomerDto(new CustomerId("c1"), "a@b.com", "A", "B", []);

        // First GetMe fails (expired), refresh succeeds, second GetMe succeeds.
        var fake = new FakeAuth
        {
            MeResults = new Queue<Result<CustomerDto>>(new[]
            {
                Result.Failure<CustomerDto>(Error.Validation("token expired")),
                Result.Success(customer),
            }),
            RefreshResult = Result.Success(refreshed),
        };
        var fn = Create(fake);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "mach_at=expired; mach_rt=valid";

        var result = await fn.Me(ctx.Request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status200OK);
        fake.RefreshCalled.ShouldBeTrue();
        ctx.Response.Headers.SetCookie.Count.ShouldBe(2); // reset to refreshed tokens
    }

    [Fact]
    public async Task AnonymousSession_success_sets_cookies()
    {
        var anon = new CustomerSession(
            "anon-at", string.Empty, DateTimeOffset.UtcNow.AddHours(1), null, "anon-1");
        var fn = Create(new FakeAuth { AnonymousResult = Result.Success(anon) });
        var ctx = new DefaultHttpContext();

        var result = await fn.AnonymousSession(ctx.Request, CancellationToken.None);

        StatusOf(result).ShouldBe(StatusCodes.Status200OK);
        ctx.Response.Headers.SetCookie.Count.ShouldBe(1); // only access cookie (no refresh token)
    }

    private sealed class FakeAuth : ICustomerAuth
    {
        public Result<CustomerSession> RegisterResult { get; init; }
        public Result<CustomerSession> LoginResult { get; init; }
        public Result<CustomerSession> RefreshResult { get; init; }
        public Result<CustomerSession> AnonymousResult { get; init; }
        public Result<CustomerDto> MeResult { get; init; }
        public Queue<Result<CustomerDto>>? MeResults { get; init; }

        public bool LoginCalled { get; private set; }
        public bool RefreshCalled { get; private set; }

        public Task<Result<CustomerSession>> RegisterAsync(RegisterRequest request, CancellationToken ct)
            => Task.FromResult(RegisterResult);

        public Task<Result<CustomerSession>> LoginAsync(Credentials credentials, CancellationToken ct)
        {
            LoginCalled = true;
            return Task.FromResult(LoginResult);
        }

        public Task<Result<CustomerSession>> RefreshAsync(string refreshToken, CancellationToken ct)
        {
            RefreshCalled = true;
            return Task.FromResult(RefreshResult);
        }

        public Task<Result<CustomerSession>> AnonymousSessionAsync(CancellationToken ct)
            => Task.FromResult(AnonymousResult);

        public Task<Result<CustomerDto>> GetMeAsync(string accessToken, CancellationToken ct)
            => Task.FromResult(MeResults is not null ? MeResults.Dequeue() : MeResult);

        public Task<Result> MergeAnonymousCartAsync(
            string anonymousId, CustomerSession customerSession, CancellationToken ct)
            => Task.FromResult(Result.Success());
    }
}
