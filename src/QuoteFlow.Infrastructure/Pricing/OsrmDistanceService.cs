using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuoteFlow.Core.Locations;
using QuoteFlow.Core.Pricing;

namespace QuoteFlow.Infrastructure.Pricing;

public class OsrmDistanceService(
    ILocationRepository locationRepository,
    HttpClient httpClient,
    ILogger<OsrmDistanceService> logger) : IDistanceService
{
    public async Task<decimal?> GetDistanceKmAsync(string originCode, string destinationCode)
    {
        if (string.Equals(originCode, destinationCode, StringComparison.OrdinalIgnoreCase))
            return 0m;

        var origin = await locationRepository.GetByCodeAsync(originCode);
        var destination = await locationRepository.GetByCodeAsync(destinationCode);

        if (origin is null || destination is null)
        {
            logger.LogWarning("Location not found: {Origin} or {Destination}", originCode, destinationCode);
            return null;
        }

        if (origin.Latitude == 0 || destination.Latitude == 0)
        {
            logger.LogWarning("Coordinates missing for {Origin} or {Destination}", originCode, destinationCode);
            return null;
        }

        try
        {
            var url = $"http://router.project-osrm.org/route/v1/driving/" +
                      $"{origin.Longitude},{origin.Latitude};" +
                      $"{destination.Longitude},{destination.Latitude}?overview=false";

            var response = await httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            var distanceMeters = doc.RootElement
                .GetProperty("routes")[0]
                .GetProperty("legs")[0]
                .GetProperty("distance")
                .GetDouble();

            return Math.Round((decimal)(distanceMeters / 1000.0), 2);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OSRM call failed for {Origin}→{Destination}", originCode, destinationCode);
            return null;
        }
    }
}
