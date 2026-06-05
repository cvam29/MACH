using System.Text;

using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Mach.Webhooks.Functions.Tests;

/// <summary>Builds in-memory <see cref="HttpRequest"/>s and executes <see cref="IResult"/>s for assertions.</summary>
internal static class Http
{
    // IResult.ExecuteAsync resolves framework services (ILoggerFactory, ...) from RequestServices.
    private static readonly IServiceProvider Services =
        new ServiceCollection().AddLogging().BuildServiceProvider();

    /// <summary>An <see cref="HttpRequest"/> backed by a buffered body and the given headers.</summary>
    public static HttpRequest Request(string body, params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext { RequestServices = Services };
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentLength = context.Request.Body.Length;
        context.Response.Body = new MemoryStream();
        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        return context.Request;
    }

    /// <summary>Executes an <see cref="IResult"/> and returns its status code and decoded text body.</summary>
    public static async Task<(int Status, string Body)> ExecuteAsync(IResult result, HttpRequest request)
    {
        var context = request.HttpContext;
        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }
}

/// <summary>
/// Fake payment gateway: HMAC verification and parsing are scripted so webhook flow tests stay
/// independent of Adyen's real signature scheme.
/// </summary>
internal sealed class FakePaymentGateway(
    bool signatureValid = true,
    Result<IReadOnlyList<PaymentNotificationDto>>? parseResult = null) : IPaymentGateway
{
    private readonly Result<IReadOnlyList<PaymentNotificationDto>> _parseResult =
        parseResult ?? Result.Success<IReadOnlyList<PaymentNotificationDto>>([]);

    public bool VerifyWebhookSignature(string rawBody, string hmacSignature) => signatureValid;

    public Result<IReadOnlyList<PaymentNotificationDto>> ParseNotification(string rawBody) => _parseResult;

    public Task<Result<PaymentSessionDto>> CreatePaymentSessionAsync(
        CartId cartId, Money amount, CancellationToken ct) => throw new NotSupportedException();

    public static PaymentNotificationDto Notification(
        string psp = "psp-1", string merchant = "order-1", string eventCode = "AUTHORISATION", bool success = true)
        => new(psp, merchant, eventCode, success ? PaymentStatus.Authorized : PaymentStatus.Refused,
            new Money(100m, "EUR"), success, DateTimeOffset.UnixEpoch);
}

/// <summary>Fake idempotency store: first claim of a key wins; subsequent claims report Completed.</summary>
internal sealed class FakeIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyState> _keys = new(StringComparer.Ordinal);

    public List<string> Began { get; } = [];
    public List<string> Completed { get; } = [];

    /// <summary>Seed a key as already-handled to simulate a redelivery.</summary>
    public void SeedCompleted(string key) => _keys[key] = IdempotencyState.Completed;

    public Task<IdempotencyState> TryBeginAsync(string key, CancellationToken ct)
    {
        if (_keys.TryGetValue(key, out var existing))
        {
            return Task.FromResult(existing);
        }

        _keys[key] = IdempotencyState.InProgress;
        Began.Add(key);
        return Task.FromResult(IdempotencyState.Began);
    }

    public Task<IdempotencyRecord?> GetExistingAsync(string key, CancellationToken ct)
        => Task.FromResult<IdempotencyRecord?>(
            _keys.TryGetValue(key, out var s) ? new IdempotencyRecord(key, s, null) : null);

    public Task CompleteWithAsync(string key, string responsePayload, CancellationToken ct)
    {
        _keys[key] = IdempotencyState.Completed;
        Completed.Add(key);
        return Task.CompletedTask;
    }
}

/// <summary>Fake message bus recording every published (topic, message) pair.</summary>
internal sealed class FakeMessageBus : IMessageBus
{
    public List<(string Topic, object Message)> Published { get; } = [];

    public Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct)
        where TMessage : notnull
    {
        Published.Add((topic, message));
        return Task.CompletedTask;
    }
}
