using System.Text.Json;
using QuoteFlow.Core.Rules;
using QuoteFlow.Core.Rules.Parameters;

namespace QuoteFlow.Application.Rules;

public class RuleService(IRuleRepository repository) : IRuleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IEnumerable<PricingRule>> GetAllAsync() =>
        repository.GetAllAsync();

    public Task<PricingRule?> GetByIdAsync(Guid id) =>
        repository.GetByIdAsync(id);

    public async Task<PricingRule> CreateAsync(PricingRule rule)
    {
        ValidateRule(rule);
        rule.Id = Guid.NewGuid();
        rule.CreatedAt = DateTimeOffset.UtcNow;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        return await repository.CreateAsync(rule);
    }

    public async Task<PricingRule?> UpdateAsync(Guid id, PricingRule rule)
    {
        ValidateRule(rule);
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        return await repository.UpdateAsync(id, rule);
    }

    public Task<bool> DeleteAsync(Guid id) =>
        repository.DeleteAsync(id);

    private static void ValidateRule(PricingRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
            throw new ArgumentException("name is required");

        if (!Enum.IsDefined(rule.RuleType))
            throw new ArgumentException(
                $"Invalid ruleType: {(int)rule.RuleType}. Valid values: {string.Join(", ", Enum.GetValues<RuleType>().Select(r => $"{(int)r}={r}"))}");

        if (rule.Priority < 0)
            throw new ArgumentException("priority must be >= 0");

        if (rule.EffectiveFrom == default)
            throw new ArgumentException("effectiveFrom is required");

        if (rule.EffectiveTo.HasValue && rule.EffectiveTo <= rule.EffectiveFrom)
            throw new ArgumentException("effectiveTo must be after effectiveFrom");

        if (string.IsNullOrWhiteSpace(rule.Parameters))
            throw new ArgumentException("parameters is required");

        ValidateParameters(rule.RuleType, rule.Parameters);
    }

    private static void ValidateParameters(RuleType ruleType, string json)
    {
        try
        {
            switch (ruleType)
            {
                case RuleType.WeightTier:
                    var wt = JsonSerializer.Deserialize<WeightTierParameters>(json, JsonOptions)
                        ?? throw new ArgumentException("Invalid WeightTier parameters");
                    if (wt.MinWeight < 0) throw new ArgumentException("minWeight must be >= 0");
                    if (wt.MaxWeight <= wt.MinWeight) throw new ArgumentException("maxWeight must be > minWeight");
                    if (wt.Price <= 0) throw new ArgumentException("price must be > 0");
                    break;

                case RuleType.RemoteAreaSurcharge:
                    var ra = JsonSerializer.Deserialize<RemoteAreaSurchargeParameters>(json, JsonOptions)
                        ?? throw new ArgumentException("Invalid RemoteAreaSurcharge parameters");
                    if (ra.AreaCodes.Count == 0) throw new ArgumentException("areaCodes must not be empty");
                    if (ra.SurchargeAmount <= 0) throw new ArgumentException("surchargeAmount must be > 0");
                    break;

                case RuleType.TimeWindowPromotion:
                    var tw = JsonSerializer.Deserialize<TimeWindowPromotionParameters>(json, JsonOptions)
                        ?? throw new ArgumentException("Invalid TimeWindowPromotion parameters");
                    if (tw.StartHour < 0 || tw.StartHour > 23) throw new ArgumentException("startHour must be 0-23");
                    if (tw.EndHour <= tw.StartHour || tw.EndHour > 24) throw new ArgumentException("endHour must be > startHour and <= 24");
                    if (tw.DaysOfWeek.Count == 0) throw new ArgumentException("daysOfWeek must not be empty");
                    if (tw.DiscountPercent <= 0) throw new ArgumentException("discountPercent must be > 0");
                    break;

                case RuleType.ExchangeRate:
                    var er = JsonSerializer.Deserialize<ExchangeRateParameters>(json, JsonOptions)
                        ?? throw new ArgumentException("Invalid ExchangeRate parameters");
                    if (string.IsNullOrWhiteSpace(er.FromCurrency)) throw new ArgumentException("fromCurrency is required");
                    if (string.IsNullOrWhiteSpace(er.ToCurrency)) throw new ArgumentException("toCurrency is required");
                    if (er.Rate <= 0) throw new ArgumentException("rate must be > 0");
                    break;

                case RuleType.FuelSurcharge:
                    var fs = JsonSerializer.Deserialize<FuelSurchargeParameters>(json, JsonOptions)
                        ?? throw new ArgumentException("Invalid FuelSurcharge parameters");
                    if (fs.PricePerLiter <= 0) throw new ArgumentException("pricePerLiter must be > 0");
                    break;

                case RuleType.VehicleType:
                    var vt = JsonSerializer.Deserialize<VehicleTypeParameters>(json, JsonOptions)
                        ?? throw new ArgumentException("Invalid VehicleType parameters");
                    if (string.IsNullOrWhiteSpace(vt.VehicleType)) throw new ArgumentException("vehicleType is required");
                    if (vt.KmPerLiter <= 0) throw new ArgumentException("kmPerLiter must be > 0");
                    if (vt.PriceMultiplier <= 0) throw new ArgumentException("priceMultiplier must be > 0");
                    break;
            }
        }
        catch (JsonException)
        {
            throw new ArgumentException($"parameters is not valid JSON for ruleType {ruleType}");
        }
    }
}
