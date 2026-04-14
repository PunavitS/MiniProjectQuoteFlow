using System.Text.Json;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Core.Rules.Parameters;

namespace QuoteFlow.Infrastructure.Pricing.Handlers;

public class WeightTierHandler : IRuleHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Order => 2;

    public void Apply(PricingContext context)
    {
        foreach (var rule in context.Rules.Where(r => r.RuleType == RuleType.WeightTier))
        {
            var p = JsonSerializer.Deserialize<WeightTierParameters>(rule.Parameters, JsonOptions);
            if (p is null) continue;

            if (context.Request.Weight >= p.MinWeight && context.Request.Weight <= p.MaxWeight)
            {
                context.BasePrice = p.Price;
                context.AppliedRules.Add(rule.Name);
                break;
            }
        }
    }
}
