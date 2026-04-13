namespace QuoteFlow.Core.Rules.Parameters;

public record ExchangeRateParameters
{
    public string FromCurrency { get; init; } = string.Empty;
    public string ToCurrency { get; init; } = string.Empty;
    public decimal Rate { get; init; }
}
