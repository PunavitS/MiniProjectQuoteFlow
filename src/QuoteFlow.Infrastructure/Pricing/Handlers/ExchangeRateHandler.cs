using System.Text.Json;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Core.Rules.Parameters;

namespace QuoteFlow.Infrastructure.Pricing.Handlers;

public class ExchangeRateHandler : IRuleHandler
{
    private const string BaseCurrency = "THB";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Order => 1;

    public void Apply(PricingContext context)
    {
        if (context.Currency.Equals(BaseCurrency, StringComparison.OrdinalIgnoreCase))
            return;

        var exchangeRules = context.Rules.Where(r => r.RuleType == RuleType.ExchangeRate).ToList();

        var toBase = FindRate(exchangeRules, context.Currency, BaseCurrency);
        var fromBase = FindRate(exchangeRules, BaseCurrency, context.Currency);

        if (toBase is not null)
        {
            context.BasePrice = Math.Round(context.BasePrice * toBase.Rate, 2);
            context.AppliedRules.Add($"ExchangeRate {context.Currency}→{BaseCurrency} ({toBase.Rate})");
        }

        if (fromBase is not null)
            context.ReverseRate = fromBase.Rate;
    }

    private static ExchangeRateParameters? FindRate(
        IEnumerable<PricingRule> rules, string from, string to)
    {
        foreach (var rule in rules)
        {
            var p = JsonSerializer.Deserialize<ExchangeRateParameters>(rule.Parameters, JsonOptions);
            if (p is not null
                && p.FromCurrency.Equals(from, StringComparison.OrdinalIgnoreCase)
                && p.ToCurrency.Equals(to, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }
}
