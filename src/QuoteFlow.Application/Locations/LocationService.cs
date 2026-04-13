using QuoteFlow.Core.Locations;

namespace QuoteFlow.Application.Locations;

public class LocationService(ILocationRepository repository) : ILocationService
{
    public Task<IEnumerable<Location>> GetAllAsync() =>
        repository.GetAllAsync();

    public Task<Location?> GetByCodeAsync(string code) =>
        repository.GetByCodeAsync(code);

    public Task<IEnumerable<Location>> GetByRegionAsync(Region region) =>
        repository.GetByRegionAsync(region);
}
