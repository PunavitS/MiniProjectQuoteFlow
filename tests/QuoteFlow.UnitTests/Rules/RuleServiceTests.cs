using FluentAssertions;
using NSubstitute;
using QuoteFlow.Application.Rules;
using QuoteFlow.Core.Rules;

namespace QuoteFlow.UnitTests.Rules;

public class RuleServiceTests
{
    private readonly IRuleRepository _repository = Substitute.For<IRuleRepository>();
    private readonly RuleService _service;

    public RuleServiceTests()
    {
        _service = new RuleService(_repository);
    }

    [Fact]
    public async Task CreateAsync_SetsIdAndTimestamps()
    {
        var rule = new PricingRule
        {
            Name = "Test Rule",
            RuleType = RuleType.WeightTier,
            Priority = 10,
            IsActive = true,
            EffectiveFrom = DateTimeOffset.UtcNow,
            Parameters = "{}"
        };

        _repository.CreateAsync(Arg.Any<PricingRule>()).Returns(call => call.Arg<PricingRule>());

        var result = await _service.CreateAsync(rule);

        result.Id.Should().NotBeEmpty();
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAt()
    {
        var id = Guid.NewGuid();
        var rule = new PricingRule { Name = "Updated Rule" };

        _repository.UpdateAsync(id, Arg.Any<PricingRule>()).Returns(call => call.ArgAt<PricingRule>(1));

        var result = await _service.UpdateAsync(id, rule);

        result!.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((PricingRule?)null);

        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_CallsRepository()
    {
        var id = Guid.NewGuid();
        _repository.DeleteAsync(id).Returns(true);

        var result = await _service.DeleteAsync(id);

        result.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(id);
    }
}
