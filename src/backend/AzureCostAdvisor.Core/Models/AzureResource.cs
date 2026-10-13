namespace AzureCostAdvisor.Core.Models;

public record AzureResource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Kind { get; init; }
    public required string Location { get; init; }
    public required string ResourceGroup { get; init; }
    public required string SubscriptionId { get; init; }
    public string? SkuName { get; init; }
    public string? SkuTier { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> Properties { get; init; } = new();
    public DateTime LastEvaluatedUtc { get; init; } = DateTime.UtcNow;
}

public record ResourceMetricSummary
{
    public required string ResourceId { get; init; }
    public required string MetricName { get; init; }
    public double Average { get; init; }
    public double Maximum { get; init; }
    public double Percentile95 { get; init; }
    public string Unit { get; init; } = "Percent";
    public int SampleCount { get; init; }
    public TimeSpan TimeGrain { get; init; } = TimeSpan.FromHours(1);
    public DateTime WindowStartUtc { get; init; }
    public DateTime WindowEndUtc { get; init; }
}
