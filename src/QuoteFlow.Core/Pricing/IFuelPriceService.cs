namespace QuoteFlow.Core.Pricing;

/// <summary>
/// Fetches real-time fuel price from an external source.
/// Returns null if the source is unavailable — callers fall back to the stored FuelSurcharge rule.
/// </summary>
public interface IFuelPriceService
{
    Task<decimal?> GetCurrentPriceAsync();
}
