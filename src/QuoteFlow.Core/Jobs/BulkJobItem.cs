namespace QuoteFlow.Core.Jobs;

public class BulkJobItem
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int RowIndex { get; set; }
    public string OriginCode { get; set; } = string.Empty;
    public string DestinationCode { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal InputBasePrice { get; set; }
    public string Currency { get; set; } = "THB";
    public decimal? BasePrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public decimal? Discount { get; set; }
    public decimal? Surcharge { get; set; }
    public string AppliedRules { get; set; } = "[]";
    public JobItemStatus Status { get; set; } = JobItemStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
