using Microsoft.Extensions.DependencyInjection;
using QuoteFlow.Application.Jobs;
using QuoteFlow.Application.Locations;
using QuoteFlow.Application.Pricing;
using QuoteFlow.Application.Rules;

namespace QuoteFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IRuleService, RuleService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<ILocationService, LocationService>();
        return services;
    }
}
