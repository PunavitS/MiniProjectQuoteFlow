using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;

namespace QuoteFlow.Infrastructure.Pricing;

public class PricingEngine(IEnumerable<IRuleHandler> handlers) : IPricingEngine
{
    private const string BaseCurrency = "THB";
    private readonly IReadOnlyList<IRuleHandler> _handlers = handlers.OrderBy(h => h.Order).ToList();

    public QuoteResult Calculate(QuoteRequest request, IEnumerable<PricingRule> rules)
    {
        var sortedRules = rules.OrderBy(r => r.Priority).ToList();
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? BaseCurrency : request.Currency;

        var context = new PricingContext
        {
            Request = request,
            Rules = sortedRules,
            Currency = currency,
            BasePrice = request.BasePrice
        };

        // Run all registered handlers in order
        foreach (var handler in _handlers)
            handler.Apply(context);

        // Convert result back to request currency
        var finalPrice = Math.Max(0m, context.BasePrice + context.Surcharge - context.Discount);

        return new QuoteResult
        {
            OriginCode = request.OriginCode,
            DestinationCode = request.DestinationCode,
            Weight = request.Weight,
            InputBasePrice = request.BasePrice,
            BasePrice = Math.Round(context.BasePrice * context.ReverseRate, 2),
            Surcharge = Math.Round(context.Surcharge * context.ReverseRate, 2),
            Discount = Math.Round(context.Discount * context.ReverseRate, 2),
            FinalPrice = Math.Round(finalPrice * context.ReverseRate, 2),
            Currency = currency.ToUpper(),
            AppliedRules = context.AppliedRules,
            CalculatedAt = DateTimeOffset.UtcNow
        };
    }
}
