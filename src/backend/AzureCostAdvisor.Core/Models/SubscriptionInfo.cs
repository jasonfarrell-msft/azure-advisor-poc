namespace AzureCostAdvisor.Core.Models;

public record SubscriptionInfo
{
    public required string SubscriptionId { get; init; }
    public required string DisplayName { get; init; }
    public string? TenantId { get; init; }
    public string State { get; init; } = "Enabled";
    public bool IsDemo { get; init; }
    public string? OptimizationFocus { get; init; }
    public string? ScenarioSummary { get; init; }
}
