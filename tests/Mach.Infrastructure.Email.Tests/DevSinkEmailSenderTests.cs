using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Infrastructure.Email;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Mach.Infrastructure.Email.Tests;

public sealed class DevSinkEmailSenderTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "mach-mail-tests", Guid.NewGuid().ToString("n"));

    [Fact]
    public async Task SendAsync_writes_well_formed_eml_with_to_subject_audience_and_body()
    {
        var sender = new DevSinkEmailSender(
            Options.Create(new EmailOptions { SinkDirectory = _tempDir, FromAddress = "shop@mach.test" }),
            new DeterministicEmailFileNameStrategy());

        var message = new EmailMessage(
            To: "customer@example.com",
            Subject: "Your order shipped",
            HtmlBody: "<html><body><h1>On its way!</h1></body></html>",
            Audience: NotificationAudience.Customer);

        var result = await sender.SendAsync(message, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var files = Directory.GetFiles(_tempDir, "*.eml");
        files.ShouldHaveSingleItem();

        var path = files[0];
        Path.GetFileName(path).ShouldContain("customer");

        var content = await File.ReadAllTextAsync(path);
        content.ShouldContain("From: shop@mach.test");
        content.ShouldContain("To: customer@example.com");
        content.ShouldContain("Subject: Your order shipped");
        content.ShouldContain("X-Mach-Audience: Customer");
        content.ShouldContain("Content-Type: text/html; charset=utf-8");
        content.ShouldContain("<h1>On its way!</h1>");
    }

    [Fact]
    public async Task SendAsync_uses_audience_in_filename()
    {
        var sender = new DevSinkEmailSender(
            Options.Create(new EmailOptions { SinkDirectory = _tempDir }),
            new DeterministicEmailFileNameStrategy());

        var message = new EmailMessage(
            "store@example.com", "New order", "<p>order</p>", NotificationAudience.Store);

        await sender.SendAsync(message, CancellationToken.None);

        Directory.GetFiles(_tempDir, "store-*.eml").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_is_deterministic_for_identical_content()
    {
        var sender = new DevSinkEmailSender(
            Options.Create(new EmailOptions { SinkDirectory = _tempDir }),
            new DeterministicEmailFileNameStrategy());

        var message = new EmailMessage(
            "supplier@example.com", "PO", "<p>po</p>", NotificationAudience.Supplier);

        await sender.SendAsync(message, CancellationToken.None);
        await sender.SendAsync(message, CancellationToken.None);

        // Same content -> same deterministic file name -> single file (overwritten).
        Directory.GetFiles(_tempDir, "*.eml").ShouldHaveSingleItem();
    }

    [Fact]
    public void Strategy_produces_stable_name_without_clock()
    {
        var strategy = new DeterministicEmailFileNameStrategy();
        var message = new EmailMessage(
            "x@y.z", "Hi", "<p>body</p>", NotificationAudience.Reception);

        var a = strategy.GetFileName(message);
        var b = strategy.GetFileName(message);

        a.ShouldBe(b);
        a.ShouldStartWith("reception-");
        a.ShouldEndWith(".eml");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
