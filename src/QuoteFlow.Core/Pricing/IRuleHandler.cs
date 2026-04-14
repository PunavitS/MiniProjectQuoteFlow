namespace QuoteFlow.Core.Pricing;

/// <summary>
/// Strategy interface for a pricing rule step.
/// To add a new rule type: create a class that implements this interface and register it in DI.
/// PricingEngine will pick it up automatically — no engine changes needed.
/// </summary>
public interface IRuleHandler
{
    /// <summary>Execution order (lower = runs first).</summary>
    int Order { get; }

    /// <summary>Apply this rule step to the pricing context.</summary>
    void Apply(PricingContext context);
}
