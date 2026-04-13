using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuoteFlow.Core.Jobs;
using QuoteFlow.Core.Locations;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Infrastructure.Jobs;
using QuoteFlow.Infrastructure.Locations;
using QuoteFlow.Infrastructure.Pricing;
using QuoteFlow.Infrastructure.Rules;

namespace QuoteFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRuleRepository, RuleRepository>();
        services.AddSingleton<IJobRepository, JobRepository>();
        services.AddSingleton<ILocationRepository, LocationRepository>();
        services.AddSingleton<IPricingEngine, PricingEngine>();
        services.AddSingleton<BulkJobWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<BulkJobWorker>());
        services.AddHttpClient<IDistanceService, OsrmDistanceService>((sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var baseUrl = config["Osrm:BaseUrl"] ?? "http://router.project-osrm.org";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        });
        return services;
    }
}
