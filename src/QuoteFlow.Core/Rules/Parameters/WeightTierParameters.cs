namespace QuoteFlow.Core.Rules.Parameters;

public record WeightTierParameters
{
    public decimal MinWeight { get; init; }
    public decimal MaxWeight { get; init; }
    public decimal Price { get; init; }
}
