using System.Text.Json;
using FluentAssertions;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;
using QuoteFlow.Infrastructure.Pricing;

namespace QuoteFlow.UnitTests.Pricing;

public class PricingEngineTests
{
    private readonly PricingEngine _engine = new();

    private static PricingRule WeightTierRule(decimal min, decimal max, decimal price, int priority = 10) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"WeightTier {min}-{max}kg",
        RuleType = RuleType.WeightTier,
        Priority = priority,
        IsActive = true,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        Parameters = JsonSerializer.Serialize(new { minWeight = min, maxWeight = max, price })
    };

    private static PricingRule SurchargeRule(string[] areaCodes, decimal amount, int priority = 20) => new()
    {
        Id = Guid.NewGuid(),
        Name = "RemoteAreaSurcharge",
        RuleType = RuleType.RemoteAreaSurcharge,
        Priority = priority,
        IsActive = true,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        Parameters = JsonSerializer.Serialize(new { areaCodes, surchargeAmount = amount })
    };

    private static PricingRule PromoRule(int startHour, int endHour, int[] days, decimal discountPercent, int priority = 30) => new()
    {
        Id = Guid.NewGuid(),
        Name = "TimeWindowPromotion",
        RuleType = RuleType.TimeWindowPromotion,
        Priority = priority,
        IsActive = true,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        Parameters = JsonSerializer.Serialize(new { startHour, endHour, daysOfWeek = days, discountPercent })
    };

    private static PricingRule ExchangeRateRule(string from, string to, decimal rate) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{from} → {to}",
        RuleType = RuleType.ExchangeRate,
        Priority = 1,
        IsActive = true,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        Parameters = JsonSerializer.Serialize(new { fromCurrency = from, toCurrency = to, rate })
    };

    [Fact]
    public void Calculate_NoRules_ReturnInputBasePrice()
    {
        var request = new QuoteRequest
        {
            OriginCode = "BKK",
            DestinationCode = "CNX",
            Weight = 3,
            BasePrice = 100
        };

        var result = _engine.Calculate(request, []);

        result.FinalPrice.Should().Be(100);
        result.Surcharge.Should().Be(0);
        result.Discount.Should().Be(0);
        result.AppliedRules.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_WeightTierMatches_OverridesBasePrice()
    {
        var request = new QuoteRequest { OriginCode = "BKK", DestinationCode = "BKK", Weight = 3, BasePrice = 50 };

        var result = _engine.Calculate(request, [WeightTierRule(0, 5, 100)]);

        result.BasePrice.Should().Be(100);
        result.FinalPrice.Should().Be(100);
        result.AppliedRules.Should().Contain("WeightTier 0-5kg");
    }

    [Fact]
    public void Calculate_WeightTierNotMatched_UsesInputBasePrice()
    {
        var request = new QuoteRequest { OriginCode = "BKK", DestinationCode = "BKK", Weight = 20, BasePrice = 250 };

        var result = _engine.Calculate(request, [WeightTierRule(0, 5, 100)]);

        result.BasePrice.Should().Be(250);
    }

    [Fact]
    public void Calculate_RemoteAreaSurcharge_AddsSurcharge()
    {
        var request = new QuoteRequest { OriginCode = "BKK", DestinationCode = "CNX", Weight = 3, BasePrice = 100 };

        var result = _engine.Calculate(request, [SurchargeRule(["CNX", "LPG"], 50)]);

        result.Surcharge.Should().Be(50);
        result.FinalPrice.Should().Be(150);
    }

    [Fact]
    public void Calculate_RemoteAreaNotMatched_NoSurcharge()
    {
        var request = new QuoteRequest { OriginCode = "BKK", DestinationCode = "BKK", Weight = 3, BasePrice = 100 };

        var result = _engine.Calculate(request, [SurchargeRule(["CNX"], 50)]);

        result.Surcharge.Should().Be(0);
        result.FinalPrice.Should().Be(100);
    }

    [Fact]
    public void Calculate_TimeWindowPromotion_AppliesDiscount()
    {
        var friday = GetNextWeekday(DayOfWeek.Friday);
        var requestedAt = new DateTimeOffset(friday.Year, friday.Month, friday.Day, 9, 0, 0, TimeSpan.Zero);

        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "BKK",
            Weight = 3, BasePrice = 100, RequestedAt = requestedAt
        };

        var result = _engine.Calculate(request, [PromoRule(8, 12, [(int)DayOfWeek.Friday], 20)]);

        result.Discount.Should().Be(20);
        result.FinalPrice.Should().Be(80);
    }

    [Fact]
    public void Calculate_AllRulesCombined_CorrectFinalPrice()
    {
        var friday = GetNextWeekday(DayOfWeek.Friday);
        var requestedAt = new DateTimeOffset(friday.Year, friday.Month, friday.Day, 10, 0, 0, TimeSpan.Zero);

        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "CNX",
            Weight = 3, BasePrice = 50, RequestedAt = requestedAt
        };

        var rules = new PricingRule[]
        {
            WeightTierRule(0, 5, 100),
            SurchargeRule(["CNX"], 50),
            PromoRule(8, 12, [(int)DayOfWeek.Friday], 20)
        };

        var result = _engine.Calculate(request, rules);

        result.BasePrice.Should().Be(100);
        result.Surcharge.Should().Be(50);
        result.Discount.Should().Be(30);
        result.FinalPrice.Should().Be(120);
        result.AppliedRules.Should().HaveCount(3);
    }

    [Fact]
    public void Calculate_FinalPriceNeverNegative()
    {
        var friday = GetNextWeekday(DayOfWeek.Friday);
        var requestedAt = new DateTimeOffset(friday.Year, friday.Month, friday.Day, 9, 0, 0, TimeSpan.Zero);

        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "BKK",
            Weight = 3, BasePrice = 10, RequestedAt = requestedAt
        };

        var result = _engine.Calculate(request, [PromoRule(8, 12, [(int)DayOfWeek.Friday], 200)]);

        result.FinalPrice.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Calculate_MultipleWeightTiers_OnlyFirstMatchApplied()
    {
        var request = new QuoteRequest { OriginCode = "BKK", DestinationCode = "BKK", Weight = 3, BasePrice = 50 };

        var rules = new[]
        {
            WeightTierRule(0, 5, 100, priority: 10),
            WeightTierRule(0, 10, 200, priority: 20)
        };

        var result = _engine.Calculate(request, rules);

        result.BasePrice.Should().Be(100);
    }

    [Fact]
    public void Calculate_ExchangeRate_ConvertsAndReturnsInRequestCurrency()
    {
        // 10 USD × 36 = 360 THB → WeightTier match → 100 THB → 100 × 0.0278 = 2.78 USD
        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "BKK",
            Weight = 3, BasePrice = 10, Currency = "USD"
        };

        var rules = new PricingRule[]
        {
            ExchangeRateRule("USD", "THB", 36m),
            ExchangeRateRule("THB", "USD", 0.0278m),
            WeightTierRule(0, 5, 100)
        };

        var result = _engine.Calculate(request, rules);

        result.Currency.Should().Be("USD");
        result.FinalPrice.Should().Be(Math.Round(100 * 0.0278m, 2));
    }

    [Fact]
    public void Calculate_CurrencyPropagatedToResult()
    {
        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "BKK",
            Weight = 3, BasePrice = 100, Currency = "USD"
        };

        var result = _engine.Calculate(request, []);

        result.Currency.Should().Be("USD");
    }

    private static PricingRule FuelSurchargeRule(decimal pricePerLiter, int priority = 40) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Fuel Price",
        RuleType = RuleType.FuelSurcharge,
        Priority = priority,
        IsActive = true,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        Parameters = JsonSerializer.Serialize(new { pricePerLiter })
    };

    private static PricingRule VehicleTypeRule(string vehicleType, decimal kmPerLiter, decimal priceMultiplier = 1.0m, int priority = 35) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"Vehicle: {vehicleType}",
        RuleType = RuleType.VehicleType,
        Priority = priority,
        IsActive = true,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        Parameters = JsonSerializer.Serialize(new { vehicleType, kmPerLiter, priceMultiplier })
    };

    [Fact]
    public void Calculate_VehicleType_AppliesMultiplier()
    {
        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "CNX",
            Weight = 3, BasePrice = 100, VehicleType = "Truck"
        };

        var result = _engine.Calculate(request, [VehicleTypeRule("Truck", kmPerLiter: 8, priceMultiplier: 1.5m)]);

        result.BasePrice.Should().Be(150);
        result.AppliedRules.Should().Contain("Vehicle: Truck");
    }

    [Fact]
    public void Calculate_VehicleTypeNotMatched_NoMultiplier()
    {
        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "CNX",
            Weight = 3, BasePrice = 100, VehicleType = "Car"
        };

        var result = _engine.Calculate(request, [VehicleTypeRule("Truck", kmPerLiter: 8, priceMultiplier: 1.5m)]);

        result.BasePrice.Should().Be(100);
    }

    [Fact]
    public void Calculate_FuelSurcharge_AddsCostBasedOnDistance()
    {
        // distance=100km, pricePerLiter=40, kmPerLiter=8 → fuel = 100/8*40 = 500
        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "CNX",
            Weight = 3, BasePrice = 100, VehicleType = "Truck", Distance = 100
        };

        var rules = new PricingRule[]
        {
            VehicleTypeRule("Truck", kmPerLiter: 8, priceMultiplier: 1.0m),
            FuelSurchargeRule(pricePerLiter: 40)
        };

        var result = _engine.Calculate(request, rules);

        result.Surcharge.Should().Be(500);
        result.FinalPrice.Should().Be(600);
    }

    [Fact]
    public void Calculate_NoVehicleType_SkipsFuelSurcharge()
    {
        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "CNX",
            Weight = 3, BasePrice = 100, Distance = 100
        };

        var result = _engine.Calculate(request, [FuelSurchargeRule(40)]);

        result.Surcharge.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Calculate_NullOrEmptyCurrency_DefaultsToTHB(string? currency)
    {
        var request = new QuoteRequest
        {
            OriginCode = "BKK", DestinationCode = "BKK",
            Weight = 3, BasePrice = 100, Currency = currency ?? string.Empty
        };

        var result = _engine.Calculate(request, []);

        result.Currency.Should().Be("THB");
        result.FinalPrice.Should().Be(100);
    }

    private static DateTime GetNextWeekday(DayOfWeek day)
    {
        var date = DateTime.UtcNow.Date;
        while (date.DayOfWeek != day) date = date.AddDays(1);
        return date;
    }
}
