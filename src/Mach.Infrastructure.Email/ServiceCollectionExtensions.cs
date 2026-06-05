using Azure.Communication.Email;
using Azure.Identity;
using Mach.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Email;

/// <summary>DI wiring for the email adapter.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the configured <see cref="IEmailSender"/> implementation, selected by
    /// <c>Email:Provider</c> (<see cref="EmailProvider.Acs"/> | <see cref="EmailProvider.DevSink"/>).
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddOptions<EmailOptions>()
            .Bind(config.GetSection(EmailOptions.SectionName));

        // Resolve provider eagerly to decide which implementation to register.
        var provider = config.GetSection(EmailOptions.SectionName)
            .GetValue<EmailProvider>(nameof(EmailOptions.Provider));

        if (provider == EmailProvider.Acs)
        {
            services.AddSingleton(sp => CreateEmailClient(sp.GetRequiredService<IOptions<EmailOptions>>().Value));
            services.AddSingleton<IEmailSender, AcsEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailFileNameStrategy, DeterministicEmailFileNameStrategy>();
            services.AddSingleton<IEmailSender, DevSinkEmailSender>();
        }

        return services;
    }

    private static EmailClient CreateEmailClient(EmailOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AcsConnectionString))
        {
            return new EmailClient(options.AcsConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(options.AcsEndpoint))
        {
            return new EmailClient(new Uri(options.AcsEndpoint), new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "ACS email provider requires either Email:AcsConnectionString or Email:AcsEndpoint (for managed identity).");
    }
}
