using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuoteFlow.Core.Jobs;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;

namespace QuoteFlow.Infrastructure.Jobs;

public class BulkJobWorker(IServiceProvider serviceProvider, ILogger<BulkJobWorker> logger)
    : BackgroundService
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();

    public async Task EnqueueAsync(Guid jobId)
    {
        await _queue.Writer.WriteAsync(jobId);
        logger.LogInformation("Job {JobId} enqueued", jobId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessJobAsync(jobId, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var ruleRepo = scope.ServiceProvider.GetRequiredService<IRuleRepository>();
        var engine = scope.ServiceProvider.GetRequiredService<IPricingEngine>();

        var job = await jobRepo.GetByIdAsync(jobId);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} not found", jobId);
            return;
        }

        job.Status = JobStatus.Processing;
        await jobRepo.UpdateAsync(job);
        logger.LogInformation("Job {JobId} processing started", jobId);

        var items = (await jobRepo.GetItemsByJobIdAsync(jobId)).ToList();
        var rules = await ruleRepo.GetActiveRulesAsync(DateTimeOffset.UtcNow);

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var request = new QuoteRequest
                {
                    OriginCode = item.OriginCode,
                    DestinationCode = item.DestinationCode,
                    Weight = item.Weight,
                    BasePrice = item.InputBasePrice,
                    Currency = item.Currency,
                    RequestedAt = DateTimeOffset.UtcNow
                };

                var result = engine.Calculate(request, rules);

                item.BasePrice = result.BasePrice;
                item.FinalPrice = result.FinalPrice;
                item.Discount = result.Discount;
                item.Surcharge = result.Surcharge;
                item.AppliedRules = JsonSerializer.Serialize(result.AppliedRules);
                item.Status = JobItemStatus.Completed;
                item.ProcessedAt = DateTimeOffset.UtcNow;

                job.ProcessedItems++;
            }
            catch (Exception ex)
            {
                item.Status = JobItemStatus.Failed;
                item.ErrorMessage = ex.Message;
                item.ProcessedAt = DateTimeOffset.UtcNow;
                job.FailedItems++;
                job.ProcessedItems++;
                logger.LogError(ex, "Failed to process item {ItemId} in job {JobId}", item.Id, jobId);
            }

            await jobRepo.UpdateItemAsync(item);
            await jobRepo.UpdateAsync(job);
        }

        job.Status = job.FailedItems == job.TotalItems && job.TotalItems > 0
            ? JobStatus.Failed
            : JobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        await jobRepo.UpdateAsync(job);

        logger.LogInformation(
            "Job {JobId} completed. Total: {Total}, Processed: {Processed}, Failed: {Failed}",
            jobId, job.TotalItems, job.ProcessedItems, job.FailedItems);
    }
}
