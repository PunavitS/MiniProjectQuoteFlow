using System.Collections.Concurrent;
using System.Text.Json;
using QuoteFlow.Core.Rules;

namespace QuoteFlow.Infrastructure.Rules;

public class RuleRepository : IRuleRepository
{
    private readonly ConcurrentDictionary<Guid, PricingRule> _store = new();

    public RuleRepository()
    {
        SeedData();
    }

    public Task<IEnumerable<PricingRule>> GetAllAsync() =>
        Task.FromResult<IEnumerable<PricingRule>>(_store.Values.OrderBy(r => r.Priority).ToList());

    public Task<PricingRule?> GetByIdAsync(Guid id) =>
        Task.FromResult(_store.TryGetValue(id, out var rule) ? rule : null);

    public Task<PricingRule> CreateAsync(PricingRule rule)
    {
        _store[rule.Id] = rule;
        return Task.FromResult(rule);
    }

    public Task<PricingRule?> UpdateAsync(Guid id, PricingRule rule)
    {
        if (!_store.ContainsKey(id))
            return Task.FromResult<PricingRule?>(null);

        rule.Id = id;
        _store[id] = rule;
        return Task.FromResult<PricingRule?>(rule);
    }

    public Task<bool> DeleteAsync(Guid id) =>
        Task.FromResult(_store.TryRemove(id, out _));

    public Task<IEnumerable<PricingRule>> GetActiveRulesAsync(DateTimeOffset at)
    {
        var active = _store.Values
            .Where(r => r.IsActive
                && r.EffectiveFrom <= at
                && (r.EffectiveTo == null || r.EffectiveTo >= at))
            .OrderBy(r => r.Priority)
            .ToList();
        return Task.FromResult<IEnumerable<PricingRule>>(active);
    }

    private void SeedData()
    {
        var rules = new List<PricingRule>
        {
            new()
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                Name = "Standard Weight Tier 0-5kg",
                Description = "Base price for shipments up to 5kg",
                RuleType = RuleType.WeightTier,
                Priority = 10,
                IsActive = true,

                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { minWeight = 0, maxWeight = 5, price = 100 }),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000002"),
                Name = "Standard Weight Tier 5-15kg",
                Description = "Base price for shipments 5-15kg",
                RuleType = RuleType.WeightTier,
                Priority = 11,
                IsActive = true,

                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { minWeight = 5.01, maxWeight = 15, price = 180 }),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000003"),
                Name = "Standard Weight Tier 15-30kg",
                Description = "Base price for shipments 15-30kg",
                RuleType = RuleType.WeightTier,
                Priority = 12,
                IsActive = true,

                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { minWeight = 15.01, maxWeight = 30, price = 300 }),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000004"),
                Name = "Remote Area Surcharge - North",
                Description = "Surcharge for northern remote areas",
                RuleType = RuleType.RemoteAreaSurcharge,
                Priority = 20,
                IsActive = true,

                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { areaCodes = new[] { "CNX", "LPG", "PYY" }, surchargeAmount = 50 }),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000005"),
                Name = "Remote Area Surcharge - South",
                Description = "Surcharge for southern remote areas",
                RuleType = RuleType.RemoteAreaSurcharge,
                Priority = 21,
                IsActive = true,

                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { areaCodes = new[] { "SGZ", "NWT", "PTN" }, surchargeAmount = 60 }),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000006"),
                Name = "Flash Sale Friday Morning",
                Description = "20% discount every Friday 8:00-12:00",
                RuleType = RuleType.TimeWindowPromotion,
                Priority = 30,
                IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { startHour = 8, endHour = 12, daysOfWeek = new[] { 5 }, discountPercent = 20 }),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },

            // Exchange Rate rules
            new() { Id = Guid.NewGuid(), Name = "USD → THB", RuleType = RuleType.ExchangeRate, Priority = 1, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { fromCurrency = "USD", toCurrency = "THB", rate = 36.00m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "THB → USD", RuleType = RuleType.ExchangeRate, Priority = 1, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { fromCurrency = "THB", toCurrency = "USD", rate = 0.0278m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "EUR → THB", RuleType = RuleType.ExchangeRate, Priority = 1, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { fromCurrency = "EUR", toCurrency = "THB", rate = 38.50m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "THB → EUR", RuleType = RuleType.ExchangeRate, Priority = 1, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { fromCurrency = "THB", toCurrency = "EUR", rate = 0.0260m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "SGD → THB", RuleType = RuleType.ExchangeRate, Priority = 1, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { fromCurrency = "SGD", toCurrency = "THB", rate = 26.50m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "THB → SGD", RuleType = RuleType.ExchangeRate, Priority = 1, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { fromCurrency = "THB", toCurrency = "SGD", rate = 0.0377m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "JPY → THB", RuleType = RuleType.ExchangeRate, Priority = 1, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { fromCurrency = "JPY", toCurrency = "THB", rate = 0.240m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "THB → JPY", RuleType = RuleType.ExchangeRate, Priority = 1, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { fromCurrency = "THB", toCurrency = "JPY", rate = 4.170m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },

            // Fuel Surcharge (ราคาน้ำมัน — admin อัปเดตได้ผ่าน PUT /rules/{id})
            new() { Id = Guid.Parse("22222222-0000-0000-0000-000000000001"), Name = "Fuel Price (THB/Liter)", RuleType = RuleType.FuelSurcharge, Priority = 40, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Description = "Current fuel price per liter in THB. Update via PUT /rules/{id} when price changes.",
                Parameters = JsonSerializer.Serialize(new { pricePerLiter = 40.50m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },

            // Vehicle Type rules
            new() { Id = Guid.Parse("33333333-0000-0000-0000-000000000001"), Name = "Vehicle: Motorcycle", RuleType = RuleType.VehicleType, Priority = 35, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { vehicleType = "Motorcycle", kmPerLiter = 35.0m, priceMultiplier = 0.8m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.Parse("33333333-0000-0000-0000-000000000002"), Name = "Vehicle: Car", RuleType = RuleType.VehicleType, Priority = 35, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { vehicleType = "Car", kmPerLiter = 14.0m, priceMultiplier = 1.0m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.Parse("33333333-0000-0000-0000-000000000003"), Name = "Vehicle: Van", RuleType = RuleType.VehicleType, Priority = 35, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { vehicleType = "Van", kmPerLiter = 10.0m, priceMultiplier = 1.2m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.Parse("33333333-0000-0000-0000-000000000004"), Name = "Vehicle: Truck", RuleType = RuleType.VehicleType, Priority = 35, IsActive = true,
                EffectiveFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = null,
                Parameters = JsonSerializer.Serialize(new { vehicleType = "Truck", kmPerLiter = 8.0m, priceMultiplier = 1.5m }),
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };

        foreach (var rule in rules)
            _store[rule.Id] = rule;
    }
}
