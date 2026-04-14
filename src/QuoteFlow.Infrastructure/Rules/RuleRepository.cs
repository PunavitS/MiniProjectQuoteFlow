using System.Collections.Concurrent;
using System.Text.Json;
using QuoteFlow.Core.Rules;

namespace QuoteFlow.Infrastructure.Rules;

public class RuleRepository : IRuleRepository
{
    private readonly ConcurrentDictionary<Guid, PricingRule> _store = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RuleRepository()
    {
        SeedFromJson();
    }

    public Task<IEnumerable<PricingRule>> GetAllAsync() =>
        Task.FromResult<IEnumerable<PricingRule>>(_store.Values.OrderBy(r => r.Priority).ToList());

    public Task<PricingRule?> GetByIdAsync(Guid id) =>
        Task.FromResult(_store.TryGetValue(id, out var rule) ? rule : null);

    public Task<PricingRule> CreateAsync(PricingRule rule)
    {
        _store[rule.Id] = rule;
        return Task.FromResult(rule);
    }

    public Task<PricingRule?> UpdateAsync(Guid id, PricingRule rule)
    {
        if (!_store.ContainsKey(id))
            return Task.FromResult<PricingRule?>(null);

        rule.Id = id;
        _store[id] = rule;
        return Task.FromResult<PricingRule?>(rule);
    }

    public Task<bool> DeleteAsync(Guid id) =>
        Task.FromResult(_store.TryRemove(id, out _));

    public Task<IEnumerable<PricingRule>> GetActiveRulesAsync(DateTimeOffset at)
    {
        var active = _store.Values
            .Where(r => r.IsActive
                && r.EffectiveFrom <= at
                && (r.EffectiveTo == null || r.EffectiveTo >= at))
            .OrderBy(r => r.Priority)
            .ToList();
        return Task.FromResult<IEnumerable<PricingRule>>(active);
    }

    private void SeedFromJson()
    {
        var assembly = typeof(RuleRepository).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "QuoteFlow.Infrastructure.Data.rules.json")!;

        var rules = JsonSerializer.Deserialize<List<PricingRule>>(stream, _jsonOptions)!;
        var now = DateTimeOffset.UtcNow;

        foreach (var rule in rules)
        {
            if (rule.Id == Guid.Empty) rule.Id = Guid.NewGuid();
            if (rule.CreatedAt == default) rule.CreatedAt = now;
            if (rule.UpdatedAt == default) rule.UpdatedAt = now;
            _store[rule.Id] = rule;
        }
    }
}
