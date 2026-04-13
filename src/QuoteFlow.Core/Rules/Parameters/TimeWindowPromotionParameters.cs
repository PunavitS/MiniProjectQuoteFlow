namespace QuoteFlow.Core.Rules.Parameters;

public record TimeWindowPromotionParameters
{
    public int StartHour { get; init; }
    public int EndHour { get; init; }
    public List<int> DaysOfWeek { get; init; } = [];
    public decimal DiscountPercent { get; init; }
}
