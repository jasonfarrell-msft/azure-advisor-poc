using System.Collections.Concurrent;
using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Engine;

public class CostAdvisorOrchestrator
{
    private readonly IEnumerable<IFinOpsDetector> _detectors;
    private readonly IPortfolioConflictResolver _conflictResolver;
    private readonly IRetailPricesClient _pricesClient;
    private readonly IAiContextExplainer _aiExplainer;
    private readonly ConcurrentDictionary<string, AnalysisResult> _analysisCache = new();

    public CostAdvisorOrchestrator(
        IEnumerable<IFinOpsDetector> detectors,
        IPortfolioConflictResolver conflictResolver,
        IRetailPricesClient pricesClient,
        IAiContextExplainer aiExplainer)
    {
        _detectors = detectors;
        _conflictResolver = conflictResolver;
        _pricesClient = pricesClient;
        _aiExplainer = aiExplainer;
    }

    public async Task<AnalysisResult> RunAnalysisAsync(
        IAzureDataProvider dataProvider,
        string subscriptionId,
        bool enrichWithAi = true,
        CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var jobId = $"job-{Guid.NewGuid():N}";

        // 1. Ingest Resources & Metrics
        var resources = await dataProvider.GetResourcesAsync(subscriptionId, ct);
        var resourceIds = resources.Select(r => r.Id).ToList();
        var metrics = await dataProvider.GetMetricsAsync(subscriptionId, resourceIds, ct);

        // 2. Run All Detectors
        var rawRecommendations = new List<RecommendationCard>();
        foreach (var detector in _detectors)
        {
            var detected = await detector.DetectAsync(resources, metrics, _pricesClient, ct);
            rawRecommendations.AddRange(detected);
        }

        // 3. Resolve Overlaps and Conflicts
        var resolved = _conflictResolver.ResolveOverlaps(rawRecommendations);

        // 4. Optionally Enrich with AI Foundry (or fallback to deterministic explanations)
        var finalRecommendations = resolved;
        if (enrichWithAi)
        {
            finalRecommendations = await _aiExplainer.EnrichRecommendationsAsync(resolved, ct);
        }

        // 5. Aggregate Financials
        var totalGross = finalRecommendations.Sum(r => r.Costs.GrossMonthlySavings);
        var totalNet = finalRecommendations.Where(r => r.Status == RecommendationStatus.Actionable)
                                           .Sum(r => r.Costs.NetMonthlySavings);

        var categorySavings = finalRecommendations
            .Where(r => r.Status == RecommendationStatus.Actionable)
            .GroupBy(r => r.Category.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Costs.NetMonthlySavings));

        var categoryCounts = finalRecommendations
            .GroupBy(r => r.Category.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var riskCounts = finalRecommendations
            .GroupBy(r => r.Risk.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new AnalysisResult
        {
            JobId = jobId,
            SubscriptionId = subscriptionId,
            Mode = dataProvider.Mode,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow,
            TotalResourcesScanned = resources.Count,
            TotalGrossSavingsMonthly = Math.Round(totalGross, 2),
            TotalNetRealizableSavingsMonthly = Math.Round(totalNet, 2),
            Recommendations = finalRecommendations.ToList(),
            CategorySavings = categorySavings,
            CategoryCounts = categoryCounts,
            RiskCounts = riskCounts
        };

        _analysisCache[jobId] = result;
        _analysisCache["latest"] = result;

        return result;
    }

    public AnalysisResult? GetLatestAnalysis()
    {
        return _analysisCache.TryGetValue("latest", out var res) ? res : null;
    }

    public async Task<AiExplanationResult> ExplainRecommendationAsync(RecommendationCard card, CancellationToken ct = default)
    {
        return await _aiExplainer.ExplainRecommendationAsync(card, ct);
    }

    public ScheduleCalculationResult CalculateCustomSchedule(
        RecommendationCard card,
        int startHour,
        int stopHour,
        bool includeWeekends)
    {
        var dailyHours = Math.Max(0, stopHour - startHour);
        var activeDaysPerWeek = includeWeekends ? 7 : 5;
        var weeklyActive = dailyHours * activeDaysPerWeek;
        var weeklyIdle = (168) - weeklyActive;

        // Active monthly hours (~730 hrs/mo)
        var monthlyActive = (int)Math.Round((weeklyActive / 168.0) * 730.0);
        var monthlyDeallocated = 730 - monthlyActive;

        // Current pure compute cost (subtracting retained storage/networking which cannot be deallocated)
        var monthlyCompute = Math.Max(0m, card.Costs.CurrentMonthlyCost - card.Costs.RetainedStorageCost - card.Costs.RetainedNetworkingCost);
        var hourlyCompute = monthlyCompute / 730m;
        var retainedStorage = card.Costs.RetainedStorageCost;

        var avoidedCompute = Math.Round(hourlyCompute * monthlyDeallocated, 2);
        var netSavings = avoidedCompute;

        return new ScheduleCalculationResult
        {
            RecommendationId = card.Id,
            WeeklyActiveHours = weeklyActive,
            WeeklyIdleHours = weeklyIdle,
            AvoidedComputeMonthlySavings = avoidedCompute,
            RetainedStorageMonthlyCost = retainedStorage,
            NewNetMonthlySavings = netSavings
        };
    }
}
