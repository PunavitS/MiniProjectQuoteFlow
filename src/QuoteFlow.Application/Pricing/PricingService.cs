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
        ValidateRequest(request);

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

    private static void ValidateRequest(QuoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OriginCode))
            throw new ArgumentException("originCode is required");

        if (string.IsNullOrWhiteSpace(request.DestinationCode))
            throw new ArgumentException("destinationCode is required");

        if (request.Weight <= 0)
            throw new ArgumentException("weight must be > 0");

        if (request.BasePrice <= 0)
            throw new ArgumentException("basePrice must be > 0");
    }
}
