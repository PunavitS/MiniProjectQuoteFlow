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
    [ProducesResponseType<IEnumerable<PricingRule>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var rules = await ruleService.GetAllAsync();
        return Ok(rules);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PricingRule>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var rule = await ruleService.GetByIdAsync(id);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost]
    [ProducesResponseType<PricingRule>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] PricingRule rule)
    {
        var created = await ruleService.CreateAsync(rule);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PricingRule>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PricingRule rule)
    {
        var updated = await ruleService.UpdateAsync(id, rule);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await ruleService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
