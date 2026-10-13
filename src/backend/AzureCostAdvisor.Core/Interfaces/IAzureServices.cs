using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Interfaces;

public interface IAzureDataProvider
{
    AdvisorMode Mode { get; }
    Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AzureResource>> GetResourcesAsync(string subscriptionId, CancellationToken ct = default);
    Task<IReadOnlyList<ResourceMetricSummary>> GetMetricsAsync(string subscriptionId, IEnumerable<string> resourceIds, CancellationToken ct = default);
}

public interface IRetailPricesClient
{
    Task<decimal> GetHourlyPriceAsync(string armSkuName, string serviceName, string region, string os = "Linux", CancellationToken ct = default);
    Task<decimal> GetMonthlyStoragePriceAsync(string skuName, string region, CancellationToken ct = default);
}

public interface IFinOpsDetector
{
    RecommendationCategory Category { get; }
    Task<IReadOnlyList<RecommendationCard>> DetectAsync(
        IReadOnlyList<AzureResource> resources,
        IReadOnlyList<ResourceMetricSummary> metrics,
        IRetailPricesClient pricesClient,
        CancellationToken ct = default);
}

public interface IPortfolioConflictResolver
{
    IReadOnlyList<RecommendationCard> ResolveOverlaps(IEnumerable<RecommendationCard> recommendations);
}

public interface IAiContextExplainer
{
    bool IsConfigured { get; }
    string DeploymentName { get; }
    string ProjectName { get; }
    Task<AiExplanationResult> ExplainRecommendationAsync(RecommendationCard card, CancellationToken ct = default);
    Task<IReadOnlyList<RecommendationCard>> EnrichRecommendationsAsync(IReadOnlyList<RecommendationCard> cards, CancellationToken ct = default);
}
