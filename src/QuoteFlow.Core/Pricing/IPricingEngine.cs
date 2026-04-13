using QuoteFlow.Core.Rules;

namespace QuoteFlow.Core.Pricing;

public interface IPricingEngine
{
    QuoteResult Calculate(QuoteRequest request, IEnumerable<PricingRule> rules);
}
