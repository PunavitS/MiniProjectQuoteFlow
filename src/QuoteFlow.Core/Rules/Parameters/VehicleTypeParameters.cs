namespace QuoteFlow.Core.Rules.Parameters;

public record VehicleTypeParameters
{
    public string VehicleType { get; init; } = string.Empty;
    public decimal KmPerLiter { get; init; }
    public decimal PriceMultiplier { get; init; } = 1.0m;
}
