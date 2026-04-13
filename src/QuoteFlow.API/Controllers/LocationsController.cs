using Microsoft.AspNetCore.Mvc;
using QuoteFlow.Application.Locations;
using QuoteFlow.Core.Locations;

namespace QuoteFlow.API.Controllers;

[ApiController]
[Route("locations")]
[Tags("Locations")]
public class LocationsController(ILocationService locationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IEnumerable<Location>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Region? region = null)
    {
        var locations = region.HasValue
            ? await locationService.GetByRegionAsync(region.Value)
            : await locationService.GetAllAsync();

        return Ok(locations);
    }

    [HttpGet("{code}")]
    [ProducesResponseType<Location>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var location = await locationService.GetByCodeAsync(code);
        return location is null ? NotFound() : Ok(location);
    }
}
