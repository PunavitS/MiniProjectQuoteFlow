using QuoteFlow.Core.Pricing;

namespace QuoteFlow.Application.Pricing;

public interface IPricingService
{
    Task<QuoteResult> CalculateAsync(QuoteRequest request);
}
