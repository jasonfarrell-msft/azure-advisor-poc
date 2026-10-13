namespace AzureCostAdvisor.Core.Models;

public record CostBreakdown
{
    public decimal CurrentMonthlyCost { get; init; }
    public decimal TargetMonthlyCost { get; init; }
    public decimal GrossMonthlySavings { get; init; }
    public decimal NetMonthlySavings { get; init; }
    public decimal RetainedStorageCost { get; init; }
    public decimal RetainedNetworkingCost { get; init; }
    public string Currency { get; init; } = "USD";
    public PricingBasis Basis { get; init; } = PricingBasis.RetailListPrice;
    public string? ExclusionReason { get; init; }
}

public record SchedulePlan
{
    public int OffPeakHoursPerWeek { get; init; } = 108;
    public int TotalHoursPerMonth { get; init; } = 730;
    public int DeallocatedHoursPerMonth { get; init; } = 468;
    public int ActiveHoursPerMonth { get; init; } = 262;
    public string TimeZone { get; init; } = "UTC";
    public List<DayOfWeek> EligibleDays { get; init; } = [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
    ];
    public int DailyStartHour { get; init; } = 7;
    public int DailyStopHour { get; init; } = 19;
}

public record EvidenceSummary
{
    public int TelemetryWindowDays { get; init; } = 14;
    public double? AverageCpuPercent { get; init; }
    public double? P95CpuPercent { get; init; }
    public double? AverageMemoryPercent { get; init; }
    public double? P95MemoryPercent { get; init; }
    public int? DaysUnattached { get; init; }
    public int? ConnectedAppsCount { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Observations { get; init; } = [];
}

public record AiExplanationResult
{
    public required string ExecutiveSummary { get; init; }
    public required string WorkloadContextAnalysis { get; init; }
    public required string RiskAssessment { get; init; }
    public List<string> ImplementationSteps { get; init; } = [];
    public List<string> RollbackStrategy { get; init; } = [];
    public required string TradeoffsAndAlternatives { get; init; }
    public bool IsAiGenerated { get; init; }
    public string? ModelUsed { get; init; }
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}

public record RecommendationCard
{
    public required string Id { get; init; }
    public required string ResourceId { get; init; }
    public required string ResourceName { get; init; }
    public required string ResourceType { get; init; }
    public required string ResourceGroup { get; init; }
    public required string SubscriptionId { get; init; }
    public required string Location { get; init; }
    public RecommendationCategory Category { get; init; }
    public RecommendationAction Action { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? CurrentSku { get; init; }
    public string? TargetSku { get; init; }
    public required CostBreakdown Costs { get; init; }
    public SchedulePlan? Schedule { get; init; }
    public RiskLevel Risk { get; init; }
    public ConfidenceLevel Confidence { get; init; }
    public RecommendationStatus Status { get; init; } = RecommendationStatus.Actionable;
    public List<string> Prerequisites { get; init; } = [];
    public List<string> RollbackGuidance { get; init; } = [];
    public List<string> WhyNotAlternatives { get; init; } = [];
    public EvidenceSummary Evidence { get; init; } = new();
    public AiExplanationResult? AiExplanation { get; init; }
}
