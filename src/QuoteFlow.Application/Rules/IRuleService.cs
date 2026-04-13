using QuoteFlow.Core.Rules;

namespace QuoteFlow.Application.Rules;

public interface IRuleService
{
    Task<IEnumerable<PricingRule>> GetAllAsync();
    Task<PricingRule?> GetByIdAsync(Guid id);
    Task<PricingRule> CreateAsync(PricingRule rule);
    Task<PricingRule?> UpdateAsync(Guid id, PricingRule rule);
    Task<bool> DeleteAsync(Guid id);
}
