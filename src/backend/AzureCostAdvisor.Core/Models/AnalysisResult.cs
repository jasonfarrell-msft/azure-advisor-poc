namespace AzureCostAdvisor.Core.Models;

public record AnalysisResult
{
    public required string JobId { get; init; }
    public required string SubscriptionId { get; init; }
    public AdvisorMode Mode { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; init; }
    public int TotalResourcesScanned { get; init; }
    public decimal TotalGrossSavingsMonthly { get; init; }
    public decimal TotalNetRealizableSavingsMonthly { get; init; }
    public List<RecommendationCard> Recommendations { get; init; } = [];
    public Dictionary<string, decimal> CategorySavings { get; init; } = new();
    public Dictionary<string, int> RiskCounts { get; init; } = new();
    public Dictionary<string, int> CategoryCounts { get; init; } = new();
}

public record ScheduleCalculationRequest
{
    public required string RecommendationId { get; init; }
    public int StartHour { get; init; }
    public int StopHour { get; init; }
    public bool IncludeWeekends { get; init; }
}

public record ScheduleCalculationResult
{
    public required string RecommendationId { get; init; }
    public int WeeklyActiveHours { get; init; }
    public int WeeklyIdleHours { get; init; }
    public decimal NewNetMonthlySavings { get; init; }
    public decimal RetainedStorageMonthlyCost { get; init; }
    public decimal AvoidedComputeMonthlySavings { get; init; }
}
