namespace QuoteFlow.Core.Pricing;

public interface IDistanceService
{
    Task<decimal?> GetDistanceKmAsync(string originCode, string destinationCode);
}
