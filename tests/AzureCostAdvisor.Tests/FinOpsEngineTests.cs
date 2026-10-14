using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AzureCostAdvisor.Core.AI;
using AzureCostAdvisor.Core.Engine;
using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;
using AzureCostAdvisor.Core.Providers;
using Xunit;

namespace AzureCostAdvisor.Tests;

public class FinOpsEngineTests
{
    private readonly IRetailPricesClient _pricingClient = new FixedRetailPricesClient();

    [Fact]
    public void PortfolioConflictResolver_ResolvesOverlappingRecommendations_PreventsDoubleCounting()
    {
        // Arrange
        var resolver = new PortfolioConflictResolver();
        const string vmResourceId = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/vm-dev-01";

        var rec1 = new RecommendationCard
        {
            Id = "rec-rightsize-1",
            ResourceId = vmResourceId,
            ResourceName = "vm-dev-01",
            ResourceType = "Microsoft.Compute/virtualMachines",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            Location = "eastus",
            Category = RecommendationCategory.Rightsizing,
            Action = RecommendationAction.Resize,
            Title = "Rightsize VM",
            Description = "Downsize underutilized VM",
            Status = RecommendationStatus.Actionable,
            Costs = new CostBreakdown
            {
                CurrentMonthlyCost = 140.16m,
                TargetMonthlyCost = 70.08m,
                GrossMonthlySavings = 70.08m,
                NetMonthlySavings = 70.08m
            }
        };

        var rec2 = new RecommendationCard
        {
            Id = "rec-schedule-1",
            ResourceId = vmResourceId,
            ResourceName = "vm-dev-01",
            ResourceType = "Microsoft.Compute/virtualMachines",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            Location = "eastus",
            Category = RecommendationCategory.Scheduling,
            Action = RecommendationAction.ScheduledShutdown,
            Title = "Schedule VM Shutdown",
            Description = "Automate deallocation during idle off-hours",
            Status = RecommendationStatus.Actionable,
            Costs = new CostBreakdown
            {
                CurrentMonthlyCost = 140.16m,
                TargetMonthlyCost = 50.45m,
                GrossMonthlySavings = 109.42m,
                RetainedStorageCost = 19.71m,
                NetMonthlySavings = 89.71m // Higher savings
            }
        };

        var recommendations = new List<RecommendationCard> { rec1, rec2 };

        // Act
        var resolved = resolver.ResolveOverlaps(recommendations);

        // Assert
        Assert.Equal(2, resolved.Count);

        var schedulingRec = resolved.Single(r => r.Category == RecommendationCategory.Scheduling);
        var rightsizingRec = resolved.Single(r => r.Category == RecommendationCategory.Rightsizing);

        // rec2 has higher savings, so it remains Actionable with full savings
        Assert.Equal(RecommendationStatus.Actionable, schedulingRec.Status);
        Assert.Equal(89.71m, schedulingRec.Costs.NetMonthlySavings);

        // rec1 is Overlapped: status set to Overlapped, savings zeroed out to prevent portfolio double-counting
        Assert.Equal(RecommendationStatus.Overlapped, rightsizingRec.Status);
        Assert.Equal(0.00m, rightsizingRec.Costs.NetMonthlySavings);
        Assert.NotNull(rightsizingRec.Costs.ExclusionReason);
        Assert.Contains("to prevent double-counting", rightsizingRec.Costs.ExclusionReason);

        // Sum of NetMonthlySavings in portfolio equals exactly 89.71m (no double counting!)
        var totalRealizableSavings = resolved.Sum(r => r.Costs.NetMonthlySavings);
        Assert.Equal(89.71m, totalRealizableSavings);
    }

    [Fact]
    public async Task RightsizingDetector_IdentifiesUnderutilizedVm_RecommendsDownsize()
    {
        // Arrange
        var detector = new RightsizingDetector();
        var underutilizedVm = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/vm-underutilized",
            Name = "vm-underutilized",
            Type = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            SkuName = "Standard_D4s_v5" // 4 vCPU, 16 GB RAM ($140.16/mo)
        };

        var metrics = new List<ResourceMetricSummary>
        {
            new()
            {
                ResourceId = underutilizedVm.Id,
                MetricName = "Percentage CPU",
                Percentile95 = 8.5,
                Average = 4.2
            },
            new()
            {
                ResourceId = underutilizedVm.Id,
                MetricName = "Available Memory Bytes",
                Percentile95 = 14.0, // Low memory utilization
                Average = 10.0
            }
        };

        // Act
        var recommendations = await detector.DetectAsync(new[] { underutilizedVm }, metrics, _pricingClient);

        // Assert
        Assert.Single(recommendations);
        var rec = recommendations[0];
        Assert.Equal(RecommendationCategory.Rightsizing, rec.Category);
        Assert.Equal("Standard_D2s_v5", rec.TargetSku);
        Assert.True(rec.Costs.NetMonthlySavings > 0);
        Assert.Equal(140.16m, rec.Costs.CurrentMonthlyCost);
        Assert.Equal(70.08m, rec.Costs.TargetMonthlyCost);
        Assert.Equal(70.08m, rec.Costs.NetMonthlySavings);
    }

    [Fact]
    public async Task RightsizingDetector_IgnoresWellUtilizedVm()
    {
        // Arrange
        var detector = new RightsizingDetector();
        var busyVm = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/vm-prod-busy",
            Name = "vm-prod-busy",
            Type = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            SkuName = "Standard_D4s_v5"
        };

        var metrics = new List<ResourceMetricSummary>
        {
            new()
            {
                ResourceId = busyVm.Id,
                MetricName = "Percentage CPU",
                Percentile95 = 78.0,
                Average = 65.0
            }
        };

        // Act
        var recommendations = await detector.DetectAsync(new[] { busyVm }, metrics, _pricingClient);

        // Assert
        Assert.Empty(recommendations);
    }

    [Fact]
    public async Task ReservationDetector_IdentifiesStableProductionVm_WithVerifiedReservationPrice()
    {
        var detector = new ReservationDetector();
        var vm = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-prod/providers/Microsoft.Compute/virtualMachines/vm-api",
            Name = "vm-api",
            Type = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            ResourceGroup = "rg-prod",
            SubscriptionId = "sub-1",
            SkuName = "Standard_D4s_v5",
            Tags = new(StringComparer.OrdinalIgnoreCase) { ["Environment"] = "Production" },
            Properties = new() { ["powerState"] = "VM running", ["osType"] = "Linux" }
        };
        var cpu = new ResourceMetricSummary
        {
            ResourceId = vm.Id,
            MetricName = "Percentage CPU",
            Average = 38,
            Percentile95 = 64,
            Maximum = 82,
            SampleCount = 336,
            WindowStartUtc = DateTime.UtcNow.AddDays(-14),
            WindowEndUtc = DateTime.UtcNow
        };

        var recommendations = await detector.DetectAsync([vm], [cpu], _pricingClient);

        var recommendation = Assert.Single(recommendations);
        Assert.Equal(RecommendationCategory.Reservation, recommendation.Category);
        Assert.Equal(RecommendationAction.PurchaseReservation, recommendation.Action);
        Assert.Equal(PricingBasis.CommitmentAdjusted, recommendation.Costs.Basis);
        Assert.Equal(52.56m, recommendation.Costs.GrossMonthlySavings);
        Assert.Equal(0m, recommendation.Costs.NetMonthlySavings);
        Assert.Equal(RecommendationStatus.ReviewRequired, recommendation.Status);
        Assert.Contains("existing reservation", recommendation.Costs.ExclusionReason);
    }

    [Fact]
    public async Task ReservationDetector_RejectsWindowsVm_WhenOsAwareSavingsAreUnavailable()
    {
        var detector = new ReservationDetector();
        var vm = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-dev/providers/Microsoft.Compute/virtualMachines/vm-dev",
            Name = "vm-dev",
            Type = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            ResourceGroup = "rg-dev",
            SubscriptionId = "sub-1",
            SkuName = "Standard_D4s_v5",
            Tags = new(StringComparer.OrdinalIgnoreCase) { ["Environment"] = "Production" },
            Properties = new() { ["powerState"] = "VM running", ["osType"] = "Windows" }
        };
        var cpu = new ResourceMetricSummary
        {
            ResourceId = vm.Id,
            MetricName = "Percentage CPU",
            Average = 38,
            Percentile95 = 64,
            SampleCount = 336,
            WindowStartUtc = DateTime.UtcNow.AddDays(-14),
            WindowEndUtc = DateTime.UtcNow
        };

        var recommendations = await detector.DetectAsync([vm], [cpu], _pricingClient);

        Assert.Empty(recommendations);
    }

    [Fact]
    public async Task SchedulingDetector_CalculatesAvoidedComputeAndRetainsStorageDrag()
    {
        // Arrange
        var detector = new SchedulingDetector();
        var devVm = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/vm-dev-01",
            Name = "vm-dev-01",
            Type = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            SkuName = "Standard_D4s_v5",
            Tags = new Dictionary<string, string> { { "Environment", "Development" } }
        };

        var metrics = new List<ResourceMetricSummary>
        {
            new()
            {
                ResourceId = devVm.Id,
                MetricName = "Percentage CPU",
                Percentile95 = 12.0,
                Average = 5.0
            }
        };

        // Act
        var recommendations = await detector.DetectAsync(new[] { devVm }, metrics, _pricingClient);

        // Assert
        Assert.Single(recommendations);
        var rec = recommendations[0];
        Assert.Equal(RecommendationCategory.Scheduling, rec.Category);
        Assert.NotNull(rec.Schedule);

        // Schedule: 7 AM - 7 PM weekdays = 60 active hours/week, 108 off-peak hours/week
        Assert.Equal(108, rec.Schedule.OffPeakHoursPerWeek);
        Assert.Equal(468, rec.Schedule.DeallocatedHoursPerMonth);

        // Retained storage drag should be included and non-zero (OS disk cost retained)
        Assert.True(rec.Costs.RetainedStorageCost > 0);
        Assert.Equal(19.71m, rec.Costs.RetainedStorageCost);

        // Avoided compute savings is net realized reduction
        Assert.Equal(rec.Costs.CurrentMonthlyCost - rec.Costs.TargetMonthlyCost, rec.Costs.NetMonthlySavings);
        Assert.True(rec.Costs.NetMonthlySavings > 0);
    }

    [Fact]
    public async Task OrphanResourceDetector_IdentifiesUnattachedDisksAndEmptyAppServicePlans()
    {
        // Arrange
        var detector = new OrphanResourceDetector();
        var unattachedDisk = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Compute/disks/disk-orphaned-data",
            Name = "disk-orphaned-data",
            Type = "Microsoft.Compute/disks",
            Location = "eastus",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            SkuName = "Premium_LRS",
            Properties = new Dictionary<string, object?>
            {
                { "diskState", "Unattached" },
                { "diskSizeGB", 128 }
            }
        };

        var emptyAsp = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Web/serverfarms/asp-empty-test",
            Name = "asp-empty-test",
            Type = "Microsoft.Web/serverfarms",
            Location = "eastus",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            SkuName = "P1v3",
            Properties = new Dictionary<string, object?>
            {
                { "numberOfSites", 0 }
            }
        };

        var resources = new[] { unattachedDisk, emptyAsp };

        // Act
        var recommendations = await detector.DetectAsync(resources, Array.Empty<ResourceMetricSummary>(), _pricingClient);

        // Assert
        Assert.Equal(2, recommendations.Count);
        Assert.All(recommendations, r => Assert.Equal(RecommendationCategory.Removal, r.Category));
        Assert.All(recommendations, r => Assert.Equal(RecommendationAction.DeleteOrphan, r.Action));

        var diskRec = recommendations.Single(r => r.ResourceId == unattachedDisk.Id);
        Assert.Equal(19.71m, diskRec.Costs.NetMonthlySavings);

        var aspRec = recommendations.Single(r => r.ResourceId == emptyAsp.Id);
        Assert.Equal(113.15m, aspRec.Costs.NetMonthlySavings);
    }

    [Fact]
    public async Task CostAdvisorOrchestrator_CalculatesCustomScheduleSavings()
    {
        // Arrange
        var demoProvider = new SyntheticDemoDataProvider();
        var orchestrator = CreateOrchestrator();

        // Run scan to populate latest result
        var scan = await orchestrator.RunAnalysisAsync(
            demoProvider,
            SyntheticDemoDataProvider.EngineeringSubscriptionId,
            enrichWithAi: false);
        var scheduleRec = scan.Recommendations.FirstOrDefault(r => r.Category == RecommendationCategory.Scheduling);
        Assert.NotNull(scheduleRec);

        // Act: simulate 8 AM to 5 PM (9 hours daily active, 5 days/week = 45h active, 123h idle)
        var customCalc = orchestrator.CalculateCustomSchedule(scheduleRec, 8, 17, includeWeekends: false);

        // Assert
        Assert.NotNull(customCalc);
        Assert.Equal(45, customCalc.WeeklyActiveHours);
        Assert.Equal(123, customCalc.WeeklyIdleHours);
        Assert.True(customCalc.NewNetMonthlySavings > 0);
        Assert.Equal(scheduleRec.Costs.RetainedStorageCost, customCalc.RetainedStorageMonthlyCost);

        // Verify that custom schedule avoided compute is derived solely from compute rate, not storage
        var monthlyCompute = scheduleRec.Costs.CurrentMonthlyCost - scheduleRec.Costs.RetainedStorageCost;
        var expectedHourlyCompute = monthlyCompute / 730m;
        var expectedDeallocatedHours = 730 - (int)Math.Round((45 / 168.0) * 730.0);
        var expectedAvoidedCompute = Math.Round(expectedHourlyCompute * expectedDeallocatedHours, 2);
        Assert.Equal(expectedAvoidedCompute, customCalc.AvoidedComputeMonthlySavings);
    }

    [Fact]
    public async Task CostAdvisorOrchestrator_IsolatesLatestAnalysisByOperatorScope()
    {
        var demoProvider = new SyntheticDemoDataProvider();
        var orchestrator = CreateOrchestrator();

        var production = await orchestrator.RunAnalysisAsync(
            demoProvider,
            SyntheticDemoDataProvider.ProductionSubscriptionId,
            enrichWithAi: false,
            analysisScope: "operator-production");
        var engineering = await orchestrator.RunAnalysisAsync(
            demoProvider,
            SyntheticDemoDataProvider.EngineeringSubscriptionId,
            enrichWithAi: false,
            analysisScope: "operator-engineering");

        Assert.Same(production, orchestrator.GetLatestAnalysis("operator-production"));
        Assert.Same(engineering, orchestrator.GetLatestAnalysis("operator-engineering"));
        Assert.Null(orchestrator.GetLatestAnalysis("operator-without-scan"));
    }

    [Fact]
    public void AiContextExplainer_RequiresAnExplicitFoundryEndpoint()
    {
        var deterministicOnly = new AiContextExplainer();
        var configured = new AiContextExplainer(
            "https://example-foundry.openai.azure.com/",
            "gpt-5.4-mini",
            "example-project");

        Assert.False(deterministicOnly.IsConfigured);
        Assert.True(configured.IsConfigured);
        Assert.Equal("gpt-5.4-mini", configured.DeploymentName);
        Assert.Equal("example-project", configured.ProjectName);
    }

    [Fact]
    public async Task SyntheticDemoDataProvider_OffersThreeSubscriptions_WithDistinctRecommendationScenarios()
    {
        var demoProvider = new SyntheticDemoDataProvider();
        var subscriptions = await demoProvider.GetSubscriptionsAsync();
        var orchestrator = CreateOrchestrator();

        Assert.Equal(3, subscriptions.Count);
        Assert.All(subscriptions, subscription => Assert.True(subscription.IsDemo));
        Assert.All(subscriptions, subscription => Assert.False(string.IsNullOrWhiteSpace(subscription.OptimizationFocus)));
        Assert.All(subscriptions, subscription => Assert.False(string.IsNullOrWhiteSpace(subscription.ScenarioSummary)));
        Assert.Equal(3, subscriptions.Select(subscription => subscription.OptimizationFocus).Distinct().Count());
        Assert.Equal(TimeSpan.FromSeconds(10), SyntheticDemoDataProvider.SimulatedScanDuration);

        var expectedRecommendations = new Dictionary<string, Dictionary<string, RecommendationCategory>>
        {
            [SyntheticDemoDataProvider.ProductionSubscriptionId] =
                new()
                {
                    ["asp-customer-portal"] = RecommendationCategory.Rightsizing,
                    ["vm-analytics-worker"] = RecommendationCategory.Rightsizing,
                    ["vm-order-processor"] = RecommendationCategory.Rightsizing,
                    ["vm-payment-api"] = RecommendationCategory.Reservation
                },
            [SyntheticDemoDataProvider.EngineeringSubscriptionId] =
                new()
                {
                    ["vm-dev-integration-runner"] = RecommendationCategory.Scheduling,
                    ["vm-qa-testing-node"] = RecommendationCategory.Scheduling
                },
            [SyntheticDemoDataProvider.LegacySubscriptionId] =
                new()
                {
                    ["asp-abandoned-poc"] = RecommendationCategory.Removal,
                    ["disk-unattached-migrated-db"] = RecommendationCategory.Removal,
                    ["disk-unattached-temp-scratch"] = RecommendationCategory.Removal,
                    ["pip-legacy-bastion-static"] = RecommendationCategory.Removal
                }
        };

        foreach (var subscription in subscriptions)
        {
            var analysis = await orchestrator.RunAnalysisAsync(
                demoProvider,
                subscription.SubscriptionId,
                enrichWithAi: false);

            var expected = expectedRecommendations[subscription.SubscriptionId];

            Assert.Equal(expected.Count, analysis.Recommendations.Count);
            Assert.All(analysis.Recommendations, recommendation =>
                Assert.Equal(expected[recommendation.ResourceName], recommendation.Category));
            Assert.Equal(
                expected.Keys.OrderBy(resourceName => resourceName, StringComparer.Ordinal).ToArray(),
                analysis.Recommendations
                    .Select(recommendation => recommendation.ResourceName)
                    .OrderBy(resourceName => resourceName, StringComparer.Ordinal)
                    .ToArray());
            Assert.True(analysis.TotalNetRealizableSavingsMonthly > 0);
        }
    }

    [Fact]
    public async Task RightsizingDetector_SuppressesRecommendation_WhenTelemetryIsMissing()
    {
        // Arrange
        var detector = new RightsizingDetector();
        var unmonitoredVm = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/vm-unmonitored",
            Name = "vm-unmonitored",
            Type = "Microsoft.Compute/virtualMachines",
            Location = "eastus",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            SkuName = "Standard_D4s_v5"
        };

        // Act: No telemetry provided
        var recommendations = await detector.DetectAsync(new[] { unmonitoredVm }, Array.Empty<ResourceMetricSummary>(), _pricingClient);

        // Assert: Must not fabricate recommendations without telemetry
        Assert.Empty(recommendations);
    }

    [Fact]
    public async Task OrphanResourceDetector_HandlesJsonElementPropertiesFromResourceGraph()
    {
        // Arrange: Emulate Azure Resource Graph deserialization producing JsonElement properties
        using var jsonDoc = System.Text.Json.JsonDocument.Parse("""
        {
            "diskState": "Unattached",
            "diskSizeGB": 256,
            "daysUnattached": 21,
            "numberOfSites": 0
        }
        """);
        var root = jsonDoc.RootElement;

        var detector = new OrphanResourceDetector();
        var diskWithJsonElements = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Compute/disks/disk-json-element",
            Name = "disk-json-element",
            Type = "Microsoft.Compute/disks",
            Location = "eastus",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            SkuName = "Premium_LRS",
            Properties = new Dictionary<string, object?>
            {
                { "diskState", LiveAzureDataProvider.ConvertJsonElement(root.GetProperty("diskState")) },
                { "diskSizeGB", LiveAzureDataProvider.ConvertJsonElement(root.GetProperty("diskSizeGB")) },
                { "daysUnattached", LiveAzureDataProvider.ConvertJsonElement(root.GetProperty("daysUnattached")) }
            }
        };

        var aspWithJsonElements = new AzureResource
        {
            Id = "/subscriptions/sub-1/resourceGroups/rg-test/providers/Microsoft.Web/serverfarms/asp-json-element",
            Name = "asp-json-element",
            Type = "Microsoft.Web/serverfarms",
            Location = "eastus",
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-1",
            SkuName = "P1v3",
            Properties = new Dictionary<string, object?>
            {
                { "numberOfSites", LiveAzureDataProvider.ConvertJsonElement(root.GetProperty("numberOfSites")) }
            }
        };

        // Act
        var recommendations = await detector.DetectAsync(
            new[] { diskWithJsonElements, aspWithJsonElements },
            Array.Empty<ResourceMetricSummary>(),
            _pricingClient);

        // Assert
        Assert.Equal(2, recommendations.Count);
        var diskRec = recommendations.Single(r => r.ResourceId == diskWithJsonElements.Id);
        Assert.Equal(38.00m, diskRec.Costs.NetMonthlySavings); // P15 256GB rate

        var aspRec = recommendations.Single(r => r.ResourceId == aspWithJsonElements.Id);
        Assert.Equal(113.15m, aspRec.Costs.NetMonthlySavings); // P1v3 rate
    }

    private static CostAdvisorOrchestrator CreateOrchestrator()
    {
        return new CostAdvisorOrchestrator(
            [
                new RightsizingDetector(),
                new ReservationDetector(),
                new SchedulingDetector(),
                new OrphanResourceDetector()
            ],
            new PortfolioConflictResolver(),
            new FixedRetailPricesClient(),
            new AiContextExplainer());
    }

    private sealed class FixedRetailPricesClient : IRetailPricesClient
    {
        private static readonly Dictionary<string, decimal> HourlyPrices = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Standard_D2s_v5"] = 0.096m,
            ["Standard_D4s_v5"] = 0.192m,
            ["Standard_D8s_v5"] = 0.384m,
            ["Standard_E8s_v5"] = 0.504m,
            ["Standard_B4ms"] = 0.166m,
            ["P1v3"] = 0.155m,
            ["P2v3"] = 0.310m
        };

        private static readonly Dictionary<string, decimal> MonthlyStoragePrices = new(StringComparer.OrdinalIgnoreCase)
        {
            ["P10"] = 19.71m,
            ["P15"] = 38.00m,
            ["P20"] = 73.22m,
            ["P30"] = 135.17m
        };

        public Task<decimal> GetHourlyPriceAsync(
            string armSkuName,
            string serviceName,
            string region,
            string os = "Linux",
            CancellationToken ct = default)
        {
            return Task.FromResult(HourlyPrices[armSkuName]);
        }

        public Task<decimal?> GetReservationHourlyPriceAsync(
            string armSkuName,
            string region,
            string term = "1 Year",
            string os = "Linux",
            CancellationToken ct = default)
        {
            return Task.FromResult<decimal?>(armSkuName.Equals("Standard_D4s_v5", StringComparison.OrdinalIgnoreCase)
                ? 0.120m
                : null);
        }

        public Task<decimal> GetMonthlyStoragePriceAsync(
            string skuName,
            string region,
            CancellationToken ct = default)
        {
            return Task.FromResult(MonthlyStoragePrices[skuName]);
        }
    }
}
