using System.Collections.Concurrent;
using QuoteFlow.Core.Jobs;

namespace QuoteFlow.Infrastructure.Jobs;

public class JobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<Guid, BulkJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, BulkJobItem> _items = new();

    public Task<BulkJob> CreateAsync(BulkJob job)
    {
        _jobs[job.Id] = job;
        return Task.FromResult(job);
    }

    public Task<BulkJob?> GetByIdAsync(Guid id) =>
        Task.FromResult(_jobs.TryGetValue(id, out var job) ? job : null);

    public Task<BulkJob> UpdateAsync(BulkJob job)
    {
        _jobs[job.Id] = job;
        return Task.FromResult(job);
    }

    public Task<IEnumerable<BulkJobItem>> GetItemsByJobIdAsync(Guid jobId)
    {
        var items = _items.Values
            .Where(i => i.JobId == jobId)
            .OrderBy(i => i.RowIndex)
            .ToList();
        return Task.FromResult<IEnumerable<BulkJobItem>>(items);
    }

    public Task<BulkJobItem> CreateItemAsync(BulkJobItem item)
    {
        _items[item.Id] = item;
        return Task.FromResult(item);
    }

    public Task<BulkJobItem> UpdateItemAsync(BulkJobItem item)
    {
        _items[item.Id] = item;
        return Task.FromResult(item);
    }
}
