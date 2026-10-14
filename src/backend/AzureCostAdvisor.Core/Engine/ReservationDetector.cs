using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Engine;

public class ReservationDetector : IFinOpsDetector
{
    public RecommendationCategory Category => RecommendationCategory.Reservation;

    public async Task<IReadOnlyList<RecommendationCard>> DetectAsync(
        IReadOnlyList<AzureResource> resources,
        IReadOnlyList<ResourceMetricSummary> metrics,
        IRetailPricesClient pricesClient,
        CancellationToken ct = default)
    {
        var recommendations = new List<RecommendationCard>();
        var metricsByResource = metrics
            .GroupBy(metric => metric.ResourceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var resource in resources)
        {
            if (!resource.Type.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(resource.SkuName) ||
                resource.SkuName.StartsWith("Standard_B", StringComparison.OrdinalIgnoreCase) ||
                !IsProduction(resource) ||
                !IsLinux(resource) ||
                !IsContinuouslyRunning(resource))
            {
                continue;
            }

            metricsByResource.TryGetValue(resource.Id, out var resourceMetrics);
            var cpuMetric = resourceMetrics?.FirstOrDefault(metric =>
                metric.MetricName.Contains("CPU", StringComparison.OrdinalIgnoreCase));

            if (cpuMetric == null ||
                cpuMetric.SampleCount < 24 * 14 ||
                cpuMetric.Average < 15 ||
                cpuMetric.Percentile95 < 30)
            {
                continue;
            }

            var payAsYouGoHourly = await pricesClient.GetHourlyPriceAsync(
                resource.SkuName,
                "Virtual Machines",
                resource.Location,
                "Linux",
                ct);
            var reservationHourly = await pricesClient.GetReservationHourlyPriceAsync(
                resource.SkuName,
                resource.Location,
                "1 Year",
                "Linux",
                ct);

            if (!reservationHourly.HasValue ||
                reservationHourly.Value <= 0 ||
                reservationHourly.Value >= payAsYouGoHourly)
            {
                continue;
            }

            var currentMonthly = Math.Round(payAsYouGoHourly * 730m, 2);
            var targetMonthly = Math.Round(reservationHourly.Value * 730m, 2);
            var savings = Math.Round(currentMonthly - targetMonthly, 2);
            if (savings <= 5m)
            {
                continue;
            }

            recommendations.Add(new RecommendationCard
            {
                Id = $"rec-reservation-{resource.Name}",
                ResourceId = resource.Id,
                ResourceName = resource.Name,
                ResourceType = resource.Type,
                ResourceGroup = resource.ResourceGroup,
                SubscriptionId = resource.SubscriptionId,
                Location = resource.Location,
                Category = RecommendationCategory.Reservation,
                Action = RecommendationAction.PurchaseReservation,
                Title = $"Evaluate a 1-year Reserved Instance for {resource.SkuName}",
                Description = $"{resource.Name} is a continuously running production VM with {cpuMetric.SampleCount} usage samples and sustained utilization. A verified 1-year reservation price for {resource.SkuName} could reduce compute spend while preserving the current VM size.",
                CurrentSku = $"{resource.SkuName} pay-as-you-go",
                TargetSku = $"{resource.SkuName} 1-year reservation",
                Costs = new CostBreakdown
                {
                    CurrentMonthlyCost = currentMonthly,
                    TargetMonthlyCost = targetMonthly,
                    GrossMonthlySavings = savings,
                    NetMonthlySavings = 0m,
                    Basis = PricingBasis.CommitmentAdjusted,
                    ExclusionReason = "Potential savings are excluded from realizable totals until existing reservation and savings-plan coverage is verified."
                },
                Risk = RiskLevel.Medium,
                Confidence = ConfidenceLevel.High,
                Status = RecommendationStatus.ReviewRequired,
                Prerequisites =
                [
                    "Validate at least 30 days of stable baseline usage in Azure Cost Management before purchase",
                    "Confirm the VM region, family, size flexibility, tenancy, and operating system match the reservation scope",
                    "Check existing reservations and savings plans to avoid duplicate commitment coverage",
                    "Select shared or subscription scope according to the organization's chargeback policy"
                ],
                RollbackGuidance =
                [
                    "Reservations do not resize or stop the VM; exchange, refund, or re-scope the reservation through Azure reservation management subject to current Microsoft limits"
                ],
                WhyNotAlternatives =
                [
                    "Rightsizing should be completed before commitment if telemetry indicates excess capacity",
                    "An Azure savings plan may be preferable when compute usage is stable but shifts across eligible regions, VM families, or services"
                ],
                Evidence = new EvidenceSummary
                {
                    TelemetryWindowDays = 14,
                    AverageCpuPercent = cpuMetric.Average,
                    P95CpuPercent = cpuMetric.Percentile95,
                    Tags = resource.Tags,
                    Observations =
                    [
                        $"Observed {cpuMetric.SampleCount} hourly CPU samples with {cpuMetric.Average:F1}% average and {cpuMetric.Percentile95:F1}% P95 utilization",
                        "Production classification and continuously running power state indicate recurring baseline demand",
                        "Retail API returned an eligible 1-year reservation price below pay-as-you-go"
                    ]
                }
            });
        }

        return recommendations;
    }

    private static bool IsProduction(AzureResource resource)
    {
        return resource.Tags.TryGetValue("Environment", out var environment) &&
               (environment.Equals("Production", StringComparison.OrdinalIgnoreCase) ||
                environment.Equals("Prod", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsContinuouslyRunning(AzureResource resource)
    {
        return resource.Properties.TryGetValue("powerState", out var powerState) &&
               powerState?.ToString()?.Contains("running", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsLinux(AzureResource resource)
    {
        return resource.Properties.TryGetValue("osType", out var osType) &&
               osType?.ToString()?.Equals("Linux", StringComparison.OrdinalIgnoreCase) == true;
    }
}
