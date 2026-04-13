namespace QuoteFlow.Core.Pricing;

public record QuoteResult
{
    public string OriginCode { get; init; } = string.Empty;
    public string DestinationCode { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public decimal InputBasePrice { get; init; }
    public decimal BasePrice { get; init; }
    public decimal Surcharge { get; init; }
    public decimal Discount { get; init; }
    public decimal FinalPrice { get; init; }
    public string Currency { get; init; } = "THB";
    public List<string> AppliedRules { get; init; } = [];
    public DateTimeOffset CalculatedAt { get; init; } = DateTimeOffset.UtcNow;
}
