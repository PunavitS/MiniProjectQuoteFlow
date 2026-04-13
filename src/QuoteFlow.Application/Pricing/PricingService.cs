using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;

namespace QuoteFlow.Application.Pricing;

public class PricingService(
    IRuleRepository ruleRepository,
    IPricingEngine pricingEngine,
    IDistanceService distanceService) : IPricingService
{
    public async Task<QuoteResult> CalculateAsync(QuoteRequest request)
    {
        var rules = await ruleRepository.GetActiveRulesAsync(request.RequestedAt);

        var enriched = request;
        if (!string.IsNullOrWhiteSpace(request.VehicleType) && request.Distance is null)
        {
            var distanceKm = await distanceService.GetDistanceKmAsync(request.OriginCode, request.DestinationCode);
            if (distanceKm is not null)
                enriched = request with { Distance = distanceKm };
        }

        return pricingEngine.Calculate(enriched, rules);
    }
}
