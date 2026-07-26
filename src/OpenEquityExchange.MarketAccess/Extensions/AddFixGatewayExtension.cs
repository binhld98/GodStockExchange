
using Microsoft.Extensions.DependencyInjection;

namespace OEE.MarketAcess.Extensions;

public static class AddFixGatewayExtension
{
    public static IServiceCollection AddFixGateway(this IServiceCollection services)
    {
        services.AddSingleton<FixApplication>();
        services.AddHostedService<FixGateway>(); // FixGateway is ONLY a background service and never injected elsewhere.

        return services;
    }
}