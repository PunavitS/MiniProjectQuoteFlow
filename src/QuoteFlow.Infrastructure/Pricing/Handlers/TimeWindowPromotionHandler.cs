using System.Text.Json;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Core.Rules.Parameters;

namespace QuoteFlow.Infrastructure.Pricing.Handlers;

public class TimeWindowPromotionHandler : IRuleHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Order => 6;

    public void Apply(PricingContext context)
    {
        foreach (var rule in context.Rules.Where(r => r.RuleType == RuleType.TimeWindowPromotion))
        {
            var p = JsonSerializer.Deserialize<TimeWindowPromotionParameters>(rule.Parameters, JsonOptions);
            if (p is null) continue;

            var hour = context.Request.RequestedAt.Hour;
            var day = (int)context.Request.RequestedAt.DayOfWeek;

            if (hour >= p.StartHour && hour < p.EndHour && p.DaysOfWeek.Contains(day))
            {
                context.Discount += (context.BasePrice + context.Surcharge) * (p.DiscountPercent / 100m);
                context.AppliedRules.Add(rule.Name);
            }
        }
    }
}
