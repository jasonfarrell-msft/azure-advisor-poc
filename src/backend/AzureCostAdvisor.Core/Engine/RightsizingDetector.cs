using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Engine;

public class RightsizingDetector : IFinOpsDetector
{
    public RecommendationCategory Category => RecommendationCategory.Rightsizing;

    private static readonly Dictionary<string, string> VmDownsizeTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Standard_D16s_v5"] = "Standard_D8s_v5",
        ["Standard_D8s_v5"] = "Standard_D4s_v5",
        ["Standard_D4s_v5"] = "Standard_D2s_v5",
        ["Standard_E16s_v5"] = "Standard_E8s_v5",
        ["Standard_E8s_v5"] = "Standard_D4s_v5", // Downsize high-memory to general purpose if memory is low
        ["Standard_E4s_v5"] = "Standard_D2s_v5",
        ["Standard_B8ms"] = "Standard_B4ms",
        ["Standard_B4ms"] = "Standard_B2ms",
        ["Standard_B2ms"] = "Standard_B2s",
    };

    private static readonly Dictionary<string, string> AppServiceDownsizeTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["P3v3"] = "P2v3",
        ["P2v3"] = "P1v3",
        ["P1v3"] = "S1",
        ["S3"] = "S2",
        ["S2"] = "S1",
        ["B2"] = "B1"
    };

    public async Task<IReadOnlyList<RecommendationCard>> DetectAsync(
        IReadOnlyList<AzureResource> resources,
        IReadOnlyList<ResourceMetricSummary> metrics,
        IRetailPricesClient pricesClient,
        CancellationToken ct = default)
    {
        var recommendations = new List<RecommendationCard>();
        var metricsByResource = metrics.GroupBy(m => m.ResourceId, StringComparer.OrdinalIgnoreCase)
                                       .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var resource in resources)
        {
            metricsByResource.TryGetValue(resource.Id, out var resMetrics);

            // 1. Virtual Machines Rightsizing
            if (resource.Type.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(resource.SkuName))
            {
                var cpuMetric = resMetrics?.FirstOrDefault(m => m.MetricName.Contains("CPU", StringComparison.OrdinalIgnoreCase));
                if (cpuMetric == null)
                {
                    // Require measured telemetry; do not assume low utilization when metrics are missing
                    continue;
                }

                var memMetric = resMetrics?.FirstOrDefault(m => m.MetricName.Contains("Memory", StringComparison.OrdinalIgnoreCase));
                var p95Cpu = cpuMetric.Percentile95;
                var p95Mem = memMetric?.Percentile95;

                if (p95Cpu < 20.0 && (!p95Mem.HasValue || p95Mem.Value < 40.0))
                {
                    if (VmDownsizeTargets.TryGetValue(resource.SkuName, out var targetSku))
                    {
                        var currentHourly = await pricesClient.GetHourlyPriceAsync(resource.SkuName, "Virtual Machines", resource.Location, "Linux", ct);
                        var targetHourly = await pricesClient.GetHourlyPriceAsync(targetSku, "Virtual Machines", resource.Location, "Linux", ct);

                        var currentMonthly = Math.Round(currentHourly * 730m, 2);
                        var targetMonthly = Math.Round(targetHourly * 730m, 2);
                        var savings = Math.Max(0m, currentMonthly - targetMonthly);

                        if (savings > 5m)
                        {
                            recommendations.Add(new RecommendationCard
                            {
                                Id = $"rec-rightsize-{resource.Name}",
                                ResourceId = resource.Id,
                                ResourceName = resource.Name,
                                ResourceType = resource.Type,
                                ResourceGroup = resource.ResourceGroup,
                                SubscriptionId = resource.SubscriptionId,
                                Location = resource.Location,
                                Category = RecommendationCategory.Rightsizing,
                                Action = RecommendationAction.Resize,
                                Title = $"Rightsize underutilized VM to {targetSku}",
                                Description = $"Virtual machine {resource.Name} shows peak P95 CPU utilization of {p95Cpu:F1}% over 14 days. Downsizing from {resource.SkuName} to {targetSku} maintains necessary headroom while reducing monthly compute spend.",
                                CurrentSku = resource.SkuName,
                                TargetSku = targetSku,
                                Costs = new CostBreakdown
                                {
                                    CurrentMonthlyCost = currentMonthly,
                                    TargetMonthlyCost = targetMonthly,
                                    GrossMonthlySavings = savings,
                                    NetMonthlySavings = savings,
                                    Basis = PricingBasis.RetailListPrice
                                },
                                Risk = RiskLevel.Low,
                                Confidence = ConfidenceLevel.High,
                                Prerequisites = [
                                    "Schedule a maintenance window (VM reboot required during SKU resize)",
                                    "Verify attached data disks and network bandwidth requirements conform to target SKU limits",
                                    "Ensure application dependencies do not require temporary local SSD if moving across VM families"
                                ],
                                RollbackGuidance = [
                                    $"If performance degradation occurs, resize back to {resource.SkuName} via Azure Portal or CLI: 'az vm resize --resource-group {resource.ResourceGroup} --name {resource.Name} --size {resource.SkuName}'"
                                ],
                                WhyNotAlternatives = [
                                    "Burstable B-series was not recommended because this workload requires consistent baseline memory and sustained CPU without credit exhaustion risk"
                                ],
                                Evidence = new EvidenceSummary
                                {
                                    TelemetryWindowDays = 14,
                                    AverageCpuPercent = cpuMetric.Average,
                                    P95CpuPercent = cpuMetric.Percentile95,
                                    AverageMemoryPercent = memMetric?.Average,
                                    P95MemoryPercent = memMetric?.Percentile95,
                                    Tags = resource.Tags,
                                    Observations = [
                                        $"Historical CPU P95 is {p95Cpu:F1}%, well below the 70% target utilization threshold",
                                        "Workload is a candidate for instant SKU modification during next deployment cycle"
                                    ]
                                }
                            });
                        }
                    }
                }
            }
            // 2. App Service Plans Rightsizing
            else if (resource.Type.Equals("Microsoft.Web/serverfarms", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(resource.SkuName))
            {
                var numberOfSites = 1;
                if (resource.Properties.TryGetValue("numberOfSites", out var ns))
                {
                    if (ns is int sitesInt) numberOfSites = sitesInt;
                    else if (int.TryParse(ns?.ToString(), out var parsedInt)) numberOfSites = parsedInt;
                }

                // If plan is not empty, check metrics
                if (numberOfSites > 0)
                {
                    var cpuMetric = resMetrics?.FirstOrDefault(m => m.MetricName.Contains("Cpu", StringComparison.OrdinalIgnoreCase));
                    if (cpuMetric == null)
                    {
                        // Require telemetry to verify underutilization
                        continue;
                    }
                    var p95Cpu = cpuMetric.Percentile95;

                    if (p95Cpu < 20.0 && AppServiceDownsizeTargets.TryGetValue(resource.SkuName, out var targetSku))
                    {
                        var currentHourly = await pricesClient.GetHourlyPriceAsync(resource.SkuName, "Azure App Service", resource.Location, "Linux", ct);
                        var targetHourly = await pricesClient.GetHourlyPriceAsync(targetSku, "Azure App Service", resource.Location, "Linux", ct);

                        var currentMonthly = Math.Round(currentHourly * 730m, 2);
                        var targetMonthly = Math.Round(targetHourly * 730m, 2);
                        var savings = Math.Max(0m, currentMonthly - targetMonthly);

                        if (savings > 5m)
                        {
                            recommendations.Add(new RecommendationCard
                            {
                                Id = $"rec-rightsize-{resource.Name}",
                                ResourceId = resource.Id,
                                ResourceName = resource.Name,
                                ResourceType = resource.Type,
                                ResourceGroup = resource.ResourceGroup,
                                SubscriptionId = resource.SubscriptionId,
                                Location = resource.Location,
                                Category = RecommendationCategory.Rightsizing,
                                Action = RecommendationAction.Resize,
                                Title = $"Rightsize App Service Plan to {targetSku}",
                                Description = $"App Service Plan {resource.Name} hosting {numberOfSites} app(s) experiences P95 CPU of {p95Cpu:F1}%. Moving from {resource.SkuName} to {targetSku} provides sufficient capacity at lower cost.",
                                CurrentSku = resource.SkuName,
                                TargetSku = targetSku,
                                Costs = new CostBreakdown
                                {
                                    CurrentMonthlyCost = currentMonthly,
                                    TargetMonthlyCost = targetMonthly,
                                    GrossMonthlySavings = savings,
                                    NetMonthlySavings = savings,
                                    Basis = PricingBasis.RetailListPrice
                                },
                                Risk = RiskLevel.Low,
                                Confidence = cpuMetric != null ? ConfidenceLevel.High : ConfidenceLevel.Medium,
                                Prerequisites = [
                                    "Verify target SKU tier supports custom domains, SSL bindings, and VNet integration if used by contained apps",
                                    "Confirm deployment slot limits if running on Standard/Premium tiers"
                                ],
                                RollbackGuidance = [
                                    $"Scale up plan back to {resource.SkuName}: 'az appservice plan update --resource-group {resource.ResourceGroup} --name {resource.Name} --sku {resource.SkuName}'"
                                ],
                                Evidence = new EvidenceSummary
                                {
                                    TelemetryWindowDays = 14,
                                    AverageCpuPercent = cpuMetric?.Average,
                                    P95CpuPercent = cpuMetric?.Percentile95,
                                    ConnectedAppsCount = numberOfSites,
                                    Tags = resource.Tags
                                }
                            });
                        }
                    }
                }
            }
        }

        return recommendations;
    }
}
