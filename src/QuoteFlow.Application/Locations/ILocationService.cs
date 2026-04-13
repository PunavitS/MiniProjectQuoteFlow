using QuoteFlow.Core.Locations;

namespace QuoteFlow.Application.Locations;

public interface ILocationService
{
    Task<IEnumerable<Location>> GetAllAsync();
    Task<Location?> GetByCodeAsync(string code);
    Task<IEnumerable<Location>> GetByRegionAsync(Region region);
}
