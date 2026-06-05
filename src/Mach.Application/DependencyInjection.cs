using FluentValidation;
using Mach.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mach.Application;

/// <summary>
/// Registers the application layer: MediatR handlers, FluentValidation validators, and the
/// pure application services. Vendor adapters and persistence are registered by their own
/// projects; this method wires only the brain.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IDeliveryQuoting, DeliveryQuoting>();
        services.AddSingleton<INotificationFanout, NotificationFanout>();

        return services;
    }
}
