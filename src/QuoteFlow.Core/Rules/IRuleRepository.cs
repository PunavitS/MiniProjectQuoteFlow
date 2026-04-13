namespace QuoteFlow.Core.Rules;

public interface IRuleRepository
{
    Task<IEnumerable<PricingRule>> GetAllAsync();
    Task<PricingRule?> GetByIdAsync(Guid id);
    Task<PricingRule> CreateAsync(PricingRule rule);
    Task<PricingRule?> UpdateAsync(Guid id, PricingRule rule);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<PricingRule>> GetActiveRulesAsync(DateTimeOffset at);
}
