using QuoteFlow.Core.Rules;

namespace QuoteFlow.Application.Rules;

public class RuleService(IRuleRepository repository) : IRuleService
{
    public Task<IEnumerable<PricingRule>> GetAllAsync() =>
        repository.GetAllAsync();

    public Task<PricingRule?> GetByIdAsync(Guid id) =>
        repository.GetByIdAsync(id);

    public async Task<PricingRule> CreateAsync(PricingRule rule)
    {
        rule.Id = Guid.NewGuid();
        rule.CreatedAt = DateTimeOffset.UtcNow;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        return await repository.CreateAsync(rule);
    }

    public async Task<PricingRule?> UpdateAsync(Guid id, PricingRule rule)
    {
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        return await repository.UpdateAsync(id, rule);
    }

    public Task<bool> DeleteAsync(Guid id) =>
        repository.DeleteAsync(id);
}
