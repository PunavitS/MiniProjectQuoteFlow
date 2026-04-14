using System.Text.Json;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Core.Rules.Parameters;

namespace QuoteFlow.Infrastructure.Pricing.Handlers;

public class RemoteAreaSurchargeHandler : IRuleHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Order => 5;

    public void Apply(PricingContext context)
    {
        foreach (var rule in context.Rules.Where(r => r.RuleType == RuleType.RemoteAreaSurcharge))
        {
            var p = JsonSerializer.Deserialize<RemoteAreaSurchargeParameters>(rule.Parameters, JsonOptions);
            if (p is null) continue;

            if (p.AreaCodes.Contains(context.Request.DestinationCode, StringComparer.OrdinalIgnoreCase))
            {
                context.Surcharge += p.SurchargeAmount;
                context.AppliedRules.Add(rule.Name);
            }
        }
    }
}
