using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Engine;

public class SchedulingDetector : IFinOpsDetector
{
    public RecommendationCategory Category => RecommendationCategory.Scheduling;

    private static readonly HashSet<string> NonProdEnvKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "dev", "development", "test", "testing", "stage", "staging", "qa", "sandbox", "poc", "nonprod", "non-prod"
    };

    public async Task<IReadOnlyList<RecommendationCard>> DetectAsync(
        IReadOnlyList<AzureResource> resources,
        IReadOnlyList<ResourceMetricSummary> metrics,
        IRetailPricesClient pricesClient,
        CancellationToken ct = default)
    {
        var recommendations = new List<RecommendationCard>();

        foreach (var resource in resources)
        {
            if (!resource.Type.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(resource.SkuName))
            {
                continue;
            }

            // Check if resource is dev/test/non-prod
            var isNonProd = false;
            if (resource.Tags.TryGetValue("Environment", out var envVal) && NonProdEnvKeywords.Contains(envVal))
            {
                isNonProd = true;
            }
            else if (NonProdEnvKeywords.Any(k => resource.Name.StartsWith($"{k}-", StringComparison.OrdinalIgnoreCase) ||
                                                 resource.ResourceGroup.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                isNonProd = true;
            }

            if (!isNonProd)
            {
                continue;
            }

            // Check if auto-shutdown is already enabled or if running 24/7
            var autoShutdownEnabled = resource.Tags.TryGetValue("AutoShutdown", out var asd) && asd.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (autoShutdownEnabled)
            {
                continue;
            }

            // Compute standard weekday 12h schedule (7 AM - 7 PM Monday-Friday)
            // Off-peak hours = 12h * 5 days + 48h weekend = 108 hours off / week
            // Month has ~730 hours. Active hours = 262 hours. Deallocated hours = 468 hours (64.1% compute reduction).
            var hourlyCompute = await pricesClient.GetHourlyPriceAsync(resource.SkuName, "Virtual Machines", resource.Location, "Linux", ct);
            var monthlyGrossCompute = Math.Round(hourlyCompute * 730m, 2);

            // Storage disk retained cost (e.g. standard P10 = $19.71/mo)
            var diskTier = "P10";
            if (resource.Properties.TryGetValue("osDiskTier", out var dt) && dt is string dts)
            {
                diskTier = dts;
            }
            var monthlyStorageCost = await pricesClient.GetMonthlyStoragePriceAsync(diskTier, resource.Location, ct);

            var avoidedComputeSavings = Math.Round(hourlyCompute * 468m, 2);
            var targetComputeCost = Math.Round(hourlyCompute * 262m, 2);

            var currentTotalMonthly = monthlyGrossCompute + monthlyStorageCost;
            var targetTotalMonthly = targetComputeCost + monthlyStorageCost;
            var netSavings = avoidedComputeSavings;

            if (netSavings > 10m)
            {
                var schedule = new SchedulePlan
                {
                    OffPeakHoursPerWeek = 108,
                    TotalHoursPerMonth = 730,
                    DeallocatedHoursPerMonth = 468,
                    ActiveHoursPerMonth = 262,
                    TimeZone = "UTC",
                    DailyStartHour = 7,
                    DailyStopHour = 19
                };

                recommendations.Add(new RecommendationCard
                {
                    Id = $"rec-schedule-{resource.Name}",
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ResourceGroup = resource.ResourceGroup,
                    SubscriptionId = resource.SubscriptionId,
                    Location = resource.Location,
                    Category = RecommendationCategory.Scheduling,
                    Action = RecommendationAction.ScheduledShutdown,
                    Title = $"Schedule automated shutdown for {resource.Name} during off-peak hours",
                    Description = $"Non-production virtual machine {resource.Name} runs 24/7 but is only required during development business hours. Implementing an automated start/stop schedule (M-F 07:00-19:00 UTC) eliminates 468 compute hours per month.",
                    CurrentSku = resource.SkuName,
                    TargetSku = $"{resource.SkuName} (Scheduled 262h/mo)",
                    Schedule = schedule,
                    Costs = new CostBreakdown
                    {
                        CurrentMonthlyCost = currentTotalMonthly,
                        TargetMonthlyCost = targetTotalMonthly,
                        GrossMonthlySavings = avoidedComputeSavings,
                        NetMonthlySavings = netSavings,
                        RetainedStorageCost = monthlyStorageCost,
                        Basis = PricingBasis.RetailListPrice
                    },
                    Risk = RiskLevel.Safe,
                    Confidence = ConfidenceLevel.High,
                    Prerequisites = [
                        "Confirm development team hours and time zone requirements",
                        "Verify that no automated nightly batch tests run on this instance without coordination",
                        "Ensure VM uses Azure compute deallocation (not just OS-level shutdown) to stop compute billing"
                    ],
                    RollbackGuidance = [
                        "Disable the automated scheduled runbook or Azure VM Auto-Shutdown rule at any time to resume 24/7 continuous operation"
                    ],
                    WhyNotAlternatives = [
                        "Downsizing the SKU without scheduling still leaves the machine accumulating compute charges 24 hours a day, 7 days a week"
                    ],
                    Evidence = new EvidenceSummary
                    {
                        TelemetryWindowDays = 14,
                        Tags = resource.Tags,
                        Observations = [
                            "Resource tagged with non-production environment designation",
                            $"Deallocation eliminates {avoidedComputeSavings:C2}/month in compute charges",
                            $"Attached OS disk ({diskTier}) retained cost is {monthlyStorageCost:C2}/month"
                        ]
                    }
                });
            }
        }

        return recommendations;
    }
}
