namespace QuoteFlow.Core.Rules.Parameters;

public record RemoteAreaSurchargeParameters
{
    public List<string> AreaCodes { get; init; } = [];
    public decimal SurchargeAmount { get; init; }
}
