using System.Collections.Concurrent;
using System.Text.Json;
using QuoteFlow.Core.Locations;

namespace QuoteFlow.Infrastructure.Locations;

public class LocationRepository : ILocationRepository
{
    private readonly ConcurrentDictionary<string, Location> _store = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LocationRepository()
    {
        SeedFromJson();
    }

    public Task<IEnumerable<Location>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Location>>(
            _store.Values.Where(l => l.IsActive).OrderBy(l => l.Region).ThenBy(l => l.Name).ToList());

    public Task<Location?> GetByCodeAsync(string code) =>
        Task.FromResult(_store.TryGetValue(code, out var loc) ? loc : null);

    public Task<Location?> GetByIdAsync(Guid id) =>
        Task.FromResult(_store.Values.FirstOrDefault(l => l.Id == id));

    public Task<IEnumerable<Location>> GetByRegionAsync(Region region) =>
        Task.FromResult<IEnumerable<Location>>(
            _store.Values.Where(l => l.IsActive && l.Region == region)
                         .OrderBy(l => l.Name).ToList());

    private void SeedFromJson()
    {
        var assembly = typeof(LocationRepository).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "QuoteFlow.Infrastructure.Data.locations.json")!;

        var locations = JsonSerializer.Deserialize<List<Location>>(stream, _jsonOptions)!;

        foreach (var loc in locations)
        {
            loc.Id = Guid.NewGuid();
            loc.IsActive = true;
            _store[loc.Code] = loc;
        }
    }
}
