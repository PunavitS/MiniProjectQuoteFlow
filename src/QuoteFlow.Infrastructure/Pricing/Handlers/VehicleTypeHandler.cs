using System.Text.Json;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Core.Rules.Parameters;

namespace QuoteFlow.Infrastructure.Pricing.Handlers;

public class VehicleTypeHandler : IRuleHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Order => 3;

    public void Apply(PricingContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Request.VehicleType))
            return;

        foreach (var rule in context.Rules.Where(r => r.RuleType == RuleType.VehicleType))
        {
            var p = JsonSerializer.Deserialize<VehicleTypeParameters>(rule.Parameters, JsonOptions);
            if (p is null) continue;

            if (string.Equals(p.VehicleType, context.Request.VehicleType, StringComparison.OrdinalIgnoreCase))
            {
                context.BasePrice = Math.Round(context.BasePrice * p.PriceMultiplier, 2);
                context.AppliedRules.Add(rule.Name);
                break;
            }
        }
    }
}
