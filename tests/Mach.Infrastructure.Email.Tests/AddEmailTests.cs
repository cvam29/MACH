using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Mach.Infrastructure.Email.Tests;

public sealed class AddEmailTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void AddEmail_resolves_DevSink_when_provider_is_DevSink()
    {
        var config = Config(new()
        {
            ["Email:Provider"] = "DevSink",
            ["Email:SinkDirectory"] = "./mail",
        });

        var sp = new ServiceCollection().AddEmail(config).BuildServiceProvider();

        sp.GetRequiredService<IEmailSender>().ShouldBeOfType<DevSinkEmailSender>();
    }

    [Fact]
    public void AddEmail_defaults_to_DevSink_when_provider_absent()
    {
        var config = Config(new());

        var sp = new ServiceCollection().AddEmail(config).BuildServiceProvider();

        sp.GetRequiredService<IEmailSender>().ShouldBeOfType<DevSinkEmailSender>();
    }

    [Fact]
    public void AddEmail_resolves_Acs_when_provider_is_Acs()
    {
        var config = Config(new()
        {
            ["Email:Provider"] = "Acs",
            ["Email:FromAddress"] = "no-reply@mach.test",
            ["Email:AcsConnectionString"] = "endpoint=https://acs.test.communication.azure.com/;accesskey=" +
                Convert.ToBase64String(new byte[32]),
        });

        var sp = new ServiceCollection().AddEmail(config).BuildServiceProvider();

        sp.GetRequiredService<IEmailSender>().ShouldBeOfType<AcsEmailSender>();
    }

    [Fact]
    public void AcsEmailSender_BuildMessage_maps_to_subject_html_and_from()
    {
        var message = new EmailMessage(
            "customer@example.com", "Hello", "<p>hi</p>", NotificationAudience.Customer);

        var acs = AcsEmailSender.BuildMessage(message, "from@mach.test");

        acs.SenderAddress.ShouldBe("from@mach.test");
        acs.Content.Subject.ShouldBe("Hello");
        acs.Content.Html.ShouldBe("<p>hi</p>");
        acs.Recipients.To.ShouldContain(r => r.Address == "customer@example.com");
    }
}
