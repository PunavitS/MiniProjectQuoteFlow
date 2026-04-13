namespace QuoteFlow.Core.Pricing;

public record QuoteRequest
{
    public string OriginCode { get; init; } = string.Empty;
    public string DestinationCode { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public decimal BasePrice { get; init; }
    public string Currency { get; init; } = "THB";
    public string? VehicleType { get; init; }
    public decimal? Distance { get; init; }
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}
