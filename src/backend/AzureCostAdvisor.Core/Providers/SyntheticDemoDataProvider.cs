using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Providers;

public class SyntheticDemoDataProvider : IAzureDataProvider
{
    public AdvisorMode Mode => AdvisorMode.Demo;

    public const string ProductionSubscriptionId = "00000000-0000-0000-0000-demo00000001";
    public const string EngineeringSubscriptionId = "00000000-0000-0000-0000-demo00000002";
    public const string LegacySubscriptionId = "00000000-0000-0000-0000-demo00000003";

    private static readonly List<SubscriptionInfo> DemoSubscriptions =
    [
        new()
        {
            SubscriptionId = ProductionSubscriptionId,
            DisplayName = "Contoso Production (Demo)",
            TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47",
            State = "Enabled",
            IsDemo = true,
            OptimizationFocus = "Rightsizing",
            ScenarioSummary = "Find oversized production VMs and an underutilized App Service plan with measured headroom for lower-cost SKUs."
        },
        new()
        {
            SubscriptionId = EngineeringSubscriptionId,
            DisplayName = "Contoso Engineering (Demo)",
            TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47",
            State = "Enabled",
            IsDemo = true,
            OptimizationFocus = "Scheduling",
            ScenarioSummary = "Identify development and QA VMs that can be deallocated outside weekday business hours while retaining storage."
        },
        new()
        {
            SubscriptionId = LegacySubscriptionId,
            DisplayName = "Contoso Legacy Estate (Demo)",
            TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47",
            State = "Enabled",
            IsDemo = true,
            OptimizationFocus = "Cleanup",
            ScenarioSummary = "Remove unattached managed disks, an empty App Service plan, and an unassociated static public IP."
        }
    ];

    public Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<SubscriptionInfo>>(DemoSubscriptions);
    }

    public Task<IReadOnlyList<AzureResource>> GetResourcesAsync(string subscriptionId, CancellationToken ct = default)
    {
        var list = new List<AzureResource>
        {
            // 1. Oversized VM in Production (Rightsizing candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-orders-prod/providers/Microsoft.Compute/virtualMachines/vm-order-processor",
                Name = "vm-order-processor",
                Type = "Microsoft.Compute/virtualMachines",
                Location = "eastus",
                ResourceGroup = "rg-orders-prod",
                SubscriptionId = subscriptionId,
                SkuName = "Standard_D8s_v5",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Environment"] = "Production",
                    ["Workload"] = "OrderProcessing",
                    ["Owner"] = "CheckoutTeam"
                },
                Properties = new()
                {
                    ["osType"] = "Linux",
                    ["osDiskSizeGb"] = 128,
                    ["osDiskTier"] = "P10",
                    ["powerState"] = "VM running"
                }
            },

            // 2. Memory-oversized VM (Rightsizing candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-analytics/providers/Microsoft.Compute/virtualMachines/vm-analytics-worker",
                Name = "vm-analytics-worker",
                Type = "Microsoft.Compute/virtualMachines",
                Location = "westus2",
                ResourceGroup = "rg-analytics",
                SubscriptionId = subscriptionId,
                SkuName = "Standard_E8s_v5",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Environment"] = "Production",
                    ["Workload"] = "DataPipeline",
                    ["Owner"] = "BI-Team"
                },
                Properties = new()
                {
                    ["osType"] = "Linux",
                    ["osDiskSizeGb"] = 256,
                    ["osDiskTier"] = "P15",
                    ["powerState"] = "VM running"
                }
            },

            // 3. Dev/Test VM running 24/7 (Scheduling candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-dev-integration/providers/Microsoft.Compute/virtualMachines/vm-dev-integration-runner",
                Name = "vm-dev-integration-runner",
                Type = "Microsoft.Compute/virtualMachines",
                Location = "eastus",
                ResourceGroup = "rg-dev-integration",
                SubscriptionId = subscriptionId,
                SkuName = "Standard_D4s_v5",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Environment"] = "dev",
                    ["Workload"] = "integration-tests",
                    ["AutoShutdown"] = "false"
                },
                Properties = new()
                {
                    ["osType"] = "Linux",
                    ["osDiskSizeGb"] = 128,
                    ["osDiskTier"] = "P10",
                    ["powerState"] = "VM running"
                }
            },

            // 4. Staging VM running 24/7 (Scheduling candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-qa-sandbox/providers/Microsoft.Compute/virtualMachines/vm-qa-testing-node",
                Name = "vm-qa-testing-node",
                Type = "Microsoft.Compute/virtualMachines",
                Location = "eastus",
                ResourceGroup = "rg-qa-sandbox",
                SubscriptionId = subscriptionId,
                SkuName = "Standard_B4ms",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Environment"] = "qa",
                    ["Workload"] = "qa-test",
                    ["Team"] = "QA"
                },
                Properties = new()
                {
                    ["osType"] = "Linux",
                    ["osDiskSizeGb"] = 128,
                    ["osDiskTier"] = "P10",
                    ["powerState"] = "VM running"
                }
            },

            // 5. Oversized App Service Plan (Rightsizing candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-web-apps/providers/Microsoft.Web/serverfarms/asp-customer-portal",
                Name = "asp-customer-portal",
                Type = "Microsoft.Web/serverfarms",
                Location = "eastus",
                ResourceGroup = "rg-web-apps",
                SubscriptionId = subscriptionId,
                SkuName = "P2v3",
                SkuTier = "PremiumV3",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Environment"] = "Production",
                    ["CostCenter"] = "CC-4100"
                },
                Properties = new()
                {
                    ["numberOfSites"] = 1,
                    ["capacity"] = 1
                }
            },

            // 6. Abandoned App Service Plan with 0 apps (Removal candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-archived-poc/providers/Microsoft.Web/serverfarms/asp-abandoned-poc",
                Name = "asp-abandoned-poc",
                Type = "Microsoft.Web/serverfarms",
                Location = "eastus2",
                ResourceGroup = "rg-archived-poc",
                SubscriptionId = subscriptionId,
                SkuName = "P1v3",
                SkuTier = "PremiumV3",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Environment"] = "Dev",
                    ["Project"] = "DeprecatedPoc"
                },
                Properties = new()
                {
                    ["numberOfSites"] = 0,
                    ["capacity"] = 1
                }
            },

            // 7. Unattached Managed Disk - 1TB P30 (Removal candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-storage-legacy/providers/Microsoft.Compute/disks/disk-unattached-migrated-db",
                Name = "disk-unattached-migrated-db",
                Type = "Microsoft.Compute/disks",
                Location = "eastus",
                ResourceGroup = "rg-storage-legacy",
                SubscriptionId = subscriptionId,
                SkuName = "P30",
                SkuTier = "Premium_LRS",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["MigratedOn"] = "2026-08-01",
                    ["Source"] = "OnPremiseMigrate"
                },
                Properties = new()
                {
                    ["diskSizeGB"] = 1024,
                    ["diskState"] = "Unattached",
                    ["daysUnattached"] = 42
                }
            },

            // 8. Unattached Managed Disk - 512GB P20 (Removal candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-qa-sandbox/providers/Microsoft.Compute/disks/disk-unattached-temp-scratch",
                Name = "disk-unattached-temp-scratch",
                Type = "Microsoft.Compute/disks",
                Location = "eastus",
                ResourceGroup = "rg-qa-sandbox",
                SubscriptionId = subscriptionId,
                SkuName = "P20",
                SkuTier = "Premium_LRS",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["CreatedFor"] = "StressTest"
                },
                Properties = new()
                {
                    ["diskSizeGB"] = 512,
                    ["diskState"] = "Unattached",
                    ["daysUnattached"] = 28
                }
            },

            // 9. Unassociated Public IP Address (Removal candidate)
            new()
            {
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/rg-networking-core/providers/Microsoft.Network/publicIPAddresses/pip-legacy-bastion-static",
                Name = "pip-legacy-bastion-static",
                Type = "Microsoft.Network/publicIPAddresses",
                Location = "eastus",
                ResourceGroup = "rg-networking-core",
                SubscriptionId = subscriptionId,
                SkuName = "Standard",
                Tags = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Purpose"] = "OldBastionHost"
                },
                Properties = new()
                {
                    ["ipConfiguration"] = null,
                    ["natGateway"] = null,
                    ["publicIPAllocationMethod"] = "Static"
                }
            }
        };

        var resourceNames = subscriptionId switch
        {
            ProductionSubscriptionId => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "vm-order-processor",
                "vm-analytics-worker",
                "asp-customer-portal"
            },
            EngineeringSubscriptionId => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "vm-dev-integration-runner",
                "vm-qa-testing-node"
            },
            LegacySubscriptionId => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "asp-abandoned-poc",
                "disk-unattached-migrated-db",
                "disk-unattached-temp-scratch",
                "pip-legacy-bastion-static"
            },
            _ => throw new ArgumentException($"Unknown demo subscription '{subscriptionId}'.", nameof(subscriptionId))
        };

        return Task.FromResult<IReadOnlyList<AzureResource>>(
            list.Where(resource => resourceNames.Contains(resource.Name)).ToList());
    }

    public Task<IReadOnlyList<ResourceMetricSummary>> GetMetricsAsync(string subscriptionId, IEnumerable<string> resourceIds, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var start = now.AddDays(-14);
        var metrics = new List<ResourceMetricSummary>();

        foreach (var id in resourceIds)
        {
            if (id.EndsWith("vm-order-processor", StringComparison.OrdinalIgnoreCase))
            {
                metrics.Add(new ResourceMetricSummary
                {
                    ResourceId = id,
                    MetricName = "Percentage CPU",
                    Average = 7.2,
                    Maximum = 24.1,
                    Percentile95 = 14.8,
                    SampleCount = 336,
                    WindowStartUtc = start,
                    WindowEndUtc = now
                });
                metrics.Add(new ResourceMetricSummary
                {
                    ResourceId = id,
                    MetricName = "Available Memory Bytes",
                    Average = 24.0, // Calculated percentage used ~ 25%
                    Maximum = 42.0,
                    Percentile95 = 33.0,
                    Unit = "Percent",
                    SampleCount = 336,
                    WindowStartUtc = start,
                    WindowEndUtc = now
                });
            }
            else if (id.EndsWith("vm-analytics-worker", StringComparison.OrdinalIgnoreCase))
            {
                metrics.Add(new ResourceMetricSummary
                {
                    ResourceId = id,
                    MetricName = "Percentage CPU",
                    Average = 5.4,
                    Maximum = 18.0,
                    Percentile95 = 11.2,
                    SampleCount = 336,
                    WindowStartUtc = start,
                    WindowEndUtc = now
                });
                metrics.Add(new ResourceMetricSummary
                {
                    ResourceId = id,
                    MetricName = "Available Memory Bytes",
                    Average = 18.0,
                    Maximum = 28.0,
                    Percentile95 = 22.0,
                    Unit = "Percent",
                    SampleCount = 336,
                    WindowStartUtc = start,
                    WindowEndUtc = now
                });
            }
            else if (id.EndsWith("vm-dev-integration-runner", StringComparison.OrdinalIgnoreCase))
            {
                // Has normal activity during business hours, but 0 off-peak
                metrics.Add(new ResourceMetricSummary
                {
                    ResourceId = id,
                    MetricName = "Percentage CPU",
                    Average = 12.0,
                    Maximum = 55.0,
                    Percentile95 = 38.0,
                    SampleCount = 336,
                    WindowStartUtc = start,
                    WindowEndUtc = now
                });
            }
            else if (id.EndsWith("vm-qa-testing-node", StringComparison.OrdinalIgnoreCase))
            {
                metrics.Add(new ResourceMetricSummary
                {
                    ResourceId = id,
                    MetricName = "Percentage CPU",
                    Average = 8.5,
                    Maximum = 40.0,
                    Percentile95 = 26.0,
                    SampleCount = 336,
                    WindowStartUtc = start,
                    WindowEndUtc = now
                });
            }
            else if (id.EndsWith("asp-customer-portal", StringComparison.OrdinalIgnoreCase))
            {
                metrics.Add(new ResourceMetricSummary
                {
                    ResourceId = id,
                    MetricName = "CpuPercentage",
                    Average = 4.8,
                    Maximum = 15.2,
                    Percentile95 = 9.1,
                    SampleCount = 336,
                    WindowStartUtc = start,
                    WindowEndUtc = now
                });
                metrics.Add(new ResourceMetricSummary
                {
                    ResourceId = id,
                    MetricName = "MemoryPercentage",
                    Average = 22.0,
                    Maximum = 34.0,
                    Percentile95 = 28.0,
                    SampleCount = 336,
                    WindowStartUtc = start,
                    WindowEndUtc = now
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ResourceMetricSummary>>(metrics);
    }
}
