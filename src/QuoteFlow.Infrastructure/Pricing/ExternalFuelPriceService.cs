using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using QuoteFlow.Core.Pricing;

namespace QuoteFlow.Infrastructure.Pricing;

public class ExternalFuelPriceService(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<ExternalFuelPriceService> logger) : IFuelPriceService
{
    private const string CacheKey = "fuel_price_per_liter";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<decimal?> GetCurrentPriceAsync()
    {
        if (cache.TryGetValue(CacheKey, out decimal cached))
            return cached;

        if (httpClient.BaseAddress is null)
            return null;

        try
        {
            var response = await httpClient.GetStringAsync("");
            using var doc = JsonDocument.Parse(response);

            // รองรับหลาย format: { "pricePerLiter": 40.50 } หรือ { "price": 40.50 }
            var root = doc.RootElement;
            decimal? price = null;

            if (root.TryGetProperty("pricePerLiter", out var p1) && p1.TryGetDecimal(out var v1))
                price = v1;
            else if (root.TryGetProperty("price", out var p2) && p2.TryGetDecimal(out var v2))
                price = v2;

            if (price is > 0)
            {
                cache.Set(CacheKey, price.Value, TimeSpan.FromHours(1));
                logger.LogInformation("Fuel price updated from external API: {Price} THB/L", price.Value);
                return price.Value;
            }

            logger.LogWarning("External fuel price API returned unexpected format");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch fuel price from external API — will use stored rule value");
        }

        return null;
    }
}
