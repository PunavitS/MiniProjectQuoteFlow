using System.Text.Json;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Core.Rules.Parameters;

namespace QuoteFlow.Infrastructure.Pricing;

public class PricingEngine : IPricingEngine
{
    private const string BaseCurrency = "THB";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public QuoteResult Calculate(QuoteRequest request, IEnumerable<PricingRule> rules)
    {
        var sortedRules = rules.OrderBy(r => r.Priority).ToList();

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? BaseCurrency : request.Currency;
        decimal basePrice = request.BasePrice;
        decimal surcharge = 0;
        decimal discount = 0;
        decimal reverseRate = 1m;
        var appliedRules = new List<string>();

        // Step 1: Currency conversion — convert request price to THB if needed
        if (!currency.Equals(BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            var toBase = FindExchangeRate(sortedRules, currency, BaseCurrency);
            var fromBase = FindExchangeRate(sortedRules, BaseCurrency, currency);

            if (toBase is not null)
            {
                basePrice = Math.Round(basePrice * toBase.Rate, 2);
                appliedRules.Add($"ExchangeRate {currency}→{BaseCurrency} ({toBase.Rate})");
            }

            if (fromBase is not null)
                reverseRate = fromBase.Rate;
        }

        // Step 2: WeightTier — overrides base price if matched
        foreach (var rule in sortedRules.Where(r => r.RuleType == RuleType.WeightTier))
        {
            var p = Deserialize<WeightTierParameters>(rule.Parameters);
            if (p is null) continue;

            if (request.Weight >= p.MinWeight && request.Weight <= p.MaxWeight)
            {
                basePrice = p.Price;
                appliedRules.Add(rule.Name);
                break;
            }
        }

        // Step 3: VehicleType — multiplies base price if vehicleType provided
        if (!string.IsNullOrWhiteSpace(request.VehicleType))
        {
            foreach (var rule in sortedRules.Where(r => r.RuleType == RuleType.VehicleType))
            {
                var p = Deserialize<VehicleTypeParameters>(rule.Parameters);
                if (p is null) continue;

                if (string.Equals(p.VehicleType, request.VehicleType, StringComparison.OrdinalIgnoreCase))
                {
                    basePrice = Math.Round(basePrice * p.PriceMultiplier, 2);
                    appliedRules.Add(rule.Name);
                    break;
                }
            }
        }

        // Step 4: FuelSurcharge — distance × (pricePerLiter / kmPerLiter)
        if (!string.IsNullOrWhiteSpace(request.VehicleType) && request.Distance is > 0)
        {
            var fuelRule = sortedRules.FirstOrDefault(r => r.RuleType == RuleType.FuelSurcharge);
            var vehicleRule = sortedRules
                .Where(r => r.RuleType == RuleType.VehicleType)
                .Select(r => Deserialize<VehicleTypeParameters>(r.Parameters))
                .FirstOrDefault(p => p is not null &&
                    string.Equals(p.VehicleType, request.VehicleType, StringComparison.OrdinalIgnoreCase));

            if (fuelRule is not null && vehicleRule is not null)
            {
                var fp = Deserialize<FuelSurchargeParameters>(fuelRule.Parameters);
                if (fp is not null && vehicleRule.KmPerLiter > 0)
                {
                    var fuelCost = Math.Round(request.Distance.Value / vehicleRule.KmPerLiter * fp.PricePerLiter, 2);
                    surcharge += fuelCost;
                    appliedRules.Add($"{fuelRule.Name} ({request.Distance}km × {fp.PricePerLiter}฿/L ÷ {vehicleRule.KmPerLiter}km/L)");
                }
            }
        }

        // Step 5: RemoteAreaSurcharge — adds surcharge based on destination
        foreach (var rule in sortedRules.Where(r => r.RuleType == RuleType.RemoteAreaSurcharge))
        {
            var p = Deserialize<RemoteAreaSurchargeParameters>(rule.Parameters);
            if (p is null) continue;

            if (p.AreaCodes.Contains(request.DestinationCode, StringComparer.OrdinalIgnoreCase))
            {
                surcharge += p.SurchargeAmount;
                appliedRules.Add(rule.Name);
            }
        }

        // Step 6: TimeWindowPromotion — discounts based on time & day
        foreach (var rule in sortedRules.Where(r => r.RuleType == RuleType.TimeWindowPromotion))
        {
            var p = Deserialize<TimeWindowPromotionParameters>(rule.Parameters);
            if (p is null) continue;

            var hour = request.RequestedAt.Hour;
            var day = (int)request.RequestedAt.DayOfWeek;

            if (hour >= p.StartHour && hour < p.EndHour && p.DaysOfWeek.Contains(day))
            {
                discount += (basePrice + surcharge) * (p.DiscountPercent / 100m);
                appliedRules.Add(rule.Name);
            }
        }

        var finalPriceInBase = Math.Max(0m, basePrice + surcharge - discount);

        // Step 7: Convert result back to request currency
        return new QuoteResult
        {
            OriginCode = request.OriginCode,
            DestinationCode = request.DestinationCode,
            Weight = request.Weight,
            InputBasePrice = request.BasePrice,
            BasePrice = Math.Round(basePrice * reverseRate, 2),
            Surcharge = Math.Round(surcharge * reverseRate, 2),
            Discount = Math.Round(discount * reverseRate, 2),
            FinalPrice = Math.Round(finalPriceInBase * reverseRate, 2),
            Currency = currency.ToUpper(),
            AppliedRules = appliedRules,
            CalculatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ExchangeRateParameters? FindExchangeRate(
        IEnumerable<PricingRule> rules, string from, string to)
    {
        foreach (var rule in rules.Where(r => r.RuleType == RuleType.ExchangeRate))
        {
            var p = Deserialize<ExchangeRateParameters>(rule.Parameters);
            if (p is not null
                && p.FromCurrency.Equals(from, StringComparison.OrdinalIgnoreCase)
                && p.ToCurrency.Equals(to, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    private static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions);
}
