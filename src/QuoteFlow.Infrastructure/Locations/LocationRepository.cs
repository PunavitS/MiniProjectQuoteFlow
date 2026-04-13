using System.Collections.Concurrent;
using QuoteFlow.Core.Locations;

namespace QuoteFlow.Infrastructure.Locations;

public class LocationRepository : ILocationRepository
{
    private readonly ConcurrentDictionary<string, Location> _store = new(StringComparer.OrdinalIgnoreCase);

    public LocationRepository()
    {
        SeedData();
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

    private void SeedData()
    {
        var locations = new List<Location>
        {
            // Central
            new() { Code = "BKK", Name = "Bangkok",           Province = "Bangkok",           Region = Region.Central,   DistanceFromBkk = 0,    IsRemoteArea = false, Latitude = 13.7563,  Longitude = 100.5018 },
            new() { Code = "NBI", Name = "Nonthaburi",        Province = "Nonthaburi",        Region = Region.Central,   DistanceFromBkk = 20,   IsRemoteArea = false, Latitude = 13.8621,  Longitude = 100.5144 },
            new() { Code = "SPK", Name = "Samut Prakan",      Province = "Samut Prakan",      Region = Region.Central,   DistanceFromBkk = 25,   IsRemoteArea = false, Latitude = 13.5990,  Longitude = 100.5998 },
            new() { Code = "NPT", Name = "Nakhon Pathom",     Province = "Nakhon Pathom",     Region = Region.Central,   DistanceFromBkk = 56,   IsRemoteArea = false, Latitude = 13.8199,  Longitude = 100.0619 },
            new() { Code = "AYA", Name = "Ayutthaya",         Province = "Phra Nakhon Si Ayutthaya", Region = Region.Central, DistanceFromBkk = 76, IsRemoteArea = false, Latitude = 14.3692, Longitude = 100.5877 },
            new() { Code = "SBR", Name = "Suphan Buri",       Province = "Suphan Buri",       Region = Region.Central,   DistanceFromBkk = 107,  IsRemoteArea = false, Latitude = 14.4744,  Longitude = 100.1178 },
            new() { Code = "LRI", Name = "Lop Buri",          Province = "Lop Buri",          Region = Region.Central,   DistanceFromBkk = 153,  IsRemoteArea = false, Latitude = 14.7995,  Longitude = 100.6534 },
            new() { Code = "SNK", Name = "Saraburi",          Province = "Saraburi",          Region = Region.Central,   DistanceFromBkk = 108,  IsRemoteArea = false, Latitude = 14.5289,  Longitude = 100.9097 },

            // North
            new() { Code = "PRE", Name = "Phrae",             Province = "Phrae",             Region = Region.North,     DistanceFromBkk = 551,  IsRemoteArea = false, Latitude = 18.1292,  Longitude = 100.1655 },
            new() { Code = "LPG", Name = "Lampang",           Province = "Lampang",           Region = Region.North,     DistanceFromBkk = 601,  IsRemoteArea = true,  Latitude = 18.2783,  Longitude = 99.5328  },
            new() { Code = "NAN", Name = "Nan",               Province = "Nan",               Region = Region.North,     DistanceFromBkk = 668,  IsRemoteArea = true,  Latitude = 18.7756,  Longitude = 100.7730 },
            new() { Code = "CNX", Name = "Chiang Mai",        Province = "Chiang Mai",        Region = Region.North,     DistanceFromBkk = 696,  IsRemoteArea = true,  Latitude = 18.7883,  Longitude = 98.9853  },
            new() { Code = "CMR", Name = "Chiang Rai",        Province = "Chiang Rai",        Region = Region.North,     DistanceFromBkk = 785,  IsRemoteArea = true,  Latitude = 19.9105,  Longitude = 99.8406  },
            new() { Code = "PYY", Name = "Mae Hong Son",      Province = "Mae Hong Son",      Region = Region.North,     DistanceFromBkk = 924,  IsRemoteArea = true,  Latitude = 19.1614,  Longitude = 97.9353  },

            // Northeast
            new() { Code = "NST", Name = "Nakhon Ratchasima", Province = "Nakhon Ratchasima", Region = Region.Northeast, DistanceFromBkk = 256,  IsRemoteArea = false, Latitude = 14.9799,  Longitude = 102.0978 },
            new() { Code = "BFV", Name = "Buri Ram",          Province = "Buri Ram",          Region = Region.Northeast, DistanceFromBkk = 410,  IsRemoteArea = false, Latitude = 14.9930,  Longitude = 103.1029 },
            new() { Code = "KKC", Name = "Khon Kaen",         Province = "Khon Kaen",         Region = Region.Northeast, DistanceFromBkk = 445,  IsRemoteArea = false, Latitude = 16.4419,  Longitude = 102.8360 },
            new() { Code = "UDN", Name = "Udon Thani",        Province = "Udon Thani",        Region = Region.Northeast, DistanceFromBkk = 564,  IsRemoteArea = false, Latitude = 17.4156,  Longitude = 102.7877 },
            new() { Code = "UBP", Name = "Ubon Ratchathani",  Province = "Ubon Ratchathani",  Region = Region.Northeast, DistanceFromBkk = 629,  IsRemoteArea = false, Latitude = 15.2448,  Longitude = 104.8473 },
            new() { Code = "MDH", Name = "Mukdahan",          Province = "Mukdahan",          Region = Region.Northeast, DistanceFromBkk = 642,  IsRemoteArea = true,  Latitude = 16.5421,  Longitude = 104.7236 },

            // East
            new() { Code = "CBI", Name = "Chon Buri",         Province = "Chon Buri",         Region = Region.East,      DistanceFromBkk = 80,   IsRemoteArea = false, Latitude = 13.3611,  Longitude = 100.9847 },
            new() { Code = "RYG", Name = "Rayong",             Province = "Rayong",            Region = Region.East,      DistanceFromBkk = 179,  IsRemoteArea = false, Latitude = 12.6814,  Longitude = 101.2816 },
            new() { Code = "TRT", Name = "Trat",               Province = "Trat",              Region = Region.East,      DistanceFromBkk = 315,  IsRemoteArea = true,  Latitude = 12.2428,  Longitude = 102.5175 },

            // West
            new() { Code = "KAN", Name = "Kanchanaburi",      Province = "Kanchanaburi",      Region = Region.West,      DistanceFromBkk = 130,  IsRemoteArea = false, Latitude = 14.0023,  Longitude = 99.5476  },
            new() { Code = "TAK", Name = "Tak",                Province = "Tak",               Region = Region.West,      DistanceFromBkk = 426,  IsRemoteArea = true,  Latitude = 16.8698,  Longitude = 99.1258  },

            // South
            new() { Code = "KBV", Name = "Krabi",             Province = "Krabi",             Region = Region.South,     DistanceFromBkk = 814,  IsRemoteArea = false, Latitude = 8.0736,   Longitude = 98.9081  },
            new() { Code = "HKT", Name = "Phuket",            Province = "Phuket",            Region = Region.South,     DistanceFromBkk = 862,  IsRemoteArea = false, Latitude = 7.8804,   Longitude = 98.3381  },
            new() { Code = "TST", Name = "Trang",             Province = "Trang",             Region = Region.South,     DistanceFromBkk = 845,  IsRemoteArea = false, Latitude = 7.5614,   Longitude = 99.6255  },
            new() { Code = "SGZ", Name = "Songkhla",          Province = "Songkhla",          Region = Region.South,     DistanceFromBkk = 950,  IsRemoteArea = true,  Latitude = 7.1897,   Longitude = 100.5955 },
            new() { Code = "PTN", Name = "Pattani",           Province = "Pattani",           Region = Region.South,     DistanceFromBkk = 1056, IsRemoteArea = true,  Latitude = 6.8674,   Longitude = 101.2500 },
            new() { Code = "NWT", Name = "Narathiwat",        Province = "Narathiwat",        Region = Region.South,     DistanceFromBkk = 1148, IsRemoteArea = true,  Latitude = 6.4254,   Longitude = 101.8236 },
        };

        foreach (var loc in locations)
        {
            loc.Id = Guid.NewGuid();
            _store[loc.Code] = loc;
        }
    }
}
