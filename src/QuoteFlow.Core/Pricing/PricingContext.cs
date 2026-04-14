using QuoteFlow.Core.Rules;

namespace QuoteFlow.Core.Pricing;

/// <summary>
/// Mutable state shared across all IRuleHandler steps during price calculation.
/// </summary>
public class PricingContext
{
    public required QuoteRequest Request { get; init; }
    public required IReadOnlyList<PricingRule> Rules { get; init; }
    public required string Currency { get; init; }
    public decimal BasePrice { get; set; }
    public decimal Surcharge { get; set; }
    public decimal Discount { get; set; }
    public decimal ReverseRate { get; set; } = 1m;
    public List<string> AppliedRules { get; } = [];
}
