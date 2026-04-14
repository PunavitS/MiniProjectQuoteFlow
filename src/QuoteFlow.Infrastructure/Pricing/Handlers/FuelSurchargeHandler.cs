using System.Text.Json;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Core.Rules.Parameters;

namespace QuoteFlow.Infrastructure.Pricing.Handlers;

public class FuelSurchargeHandler : IRuleHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Order => 4;

    public void Apply(PricingContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Request.VehicleType) || context.Request.Distance is not > 0)
            return;

        var fuelRule = context.Rules.FirstOrDefault(r => r.RuleType == RuleType.FuelSurcharge);
        var vehicleParams = context.Rules
            .Where(r => r.RuleType == RuleType.VehicleType)
            .Select(r => JsonSerializer.Deserialize<VehicleTypeParameters>(r.Parameters, JsonOptions))
            .FirstOrDefault(p => p is not null &&
                string.Equals(p.VehicleType, context.Request.VehicleType, StringComparison.OrdinalIgnoreCase));

        if (fuelRule is null || vehicleParams is null)
            return;

        var fp = JsonSerializer.Deserialize<FuelSurchargeParameters>(fuelRule.Parameters, JsonOptions);
        if (fp is null || vehicleParams.KmPerLiter <= 0)
            return;

        var fuelCost = Math.Round(context.Request.Distance.Value / vehicleParams.KmPerLiter * fp.PricePerLiter, 2);
        context.Surcharge += fuelCost;
        context.AppliedRules.Add(
            $"{fuelRule.Name} ({context.Request.Distance}km × {fp.PricePerLiter}฿/L ÷ {vehicleParams.KmPerLiter}km/L)");
    }
}
