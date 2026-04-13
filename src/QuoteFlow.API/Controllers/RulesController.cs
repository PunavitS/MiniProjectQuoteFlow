using Microsoft.AspNetCore.Mvc;
using QuoteFlow.Application.Rules;
using QuoteFlow.Core.Rules;

namespace QuoteFlow.API.Controllers;

[ApiController]
[Route("rules")]
[Tags("Rules")]
public class RulesController(IRuleService ruleService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List all rules")]
    [EndpointDescription("Returns all pricing rules ordered by priority.")]
    [ProducesResponseType<IEnumerable<PricingRule>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var rules = await ruleService.GetAllAsync();
        return Ok(rules);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get rule by ID")]
    [ProducesResponseType<PricingRule>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var rule = await ruleService.GetByIdAsync(id);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost]
    [EndpointSummary("Create rule")]
    [EndpointDescription("Create a new pricing rule. ruleType: 0=TimeWindowPromotion, 1=RemoteAreaSurcharge, 2=WeightTier, 3=ExchangeRate, 4=FuelSurcharge, 5=VehicleType")]
    [ProducesResponseType<PricingRule>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] PricingRule rule)
    {
        var created = await ruleService.CreateAsync(rule);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Update rule")]
    [EndpointDescription("Update an existing rule. Use isActive=false to disable without deleting.")]
    [ProducesResponseType<PricingRule>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PricingRule rule)
    {
        var updated = await ruleService.UpdateAsync(id, rule);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete rule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await ruleService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
