namespace QuoteFlow.Core.Locations;

public interface ILocationRepository
{
    Task<IEnumerable<Location>> GetAllAsync();
    Task<Location?> GetByCodeAsync(string code);
    Task<Location?> GetByIdAsync(Guid id);
    Task<IEnumerable<Location>> GetByRegionAsync(Region region);
}
