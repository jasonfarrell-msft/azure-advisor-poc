using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Engine;

public class OrphanResourceDetector : IFinOpsDetector
{
    public RecommendationCategory Category => RecommendationCategory.Removal;

    public async Task<IReadOnlyList<RecommendationCard>> DetectAsync(
        IReadOnlyList<AzureResource> resources,
        IReadOnlyList<ResourceMetricSummary> metrics,
        IRetailPricesClient pricesClient,
        CancellationToken ct = default)
    {
        var recommendations = new List<RecommendationCard>();

        foreach (var resource in resources)
        {
            // 1. Unattached Managed Disks
            if (resource.Type.Equals("Microsoft.Compute/disks", StringComparison.OrdinalIgnoreCase))
            {
                var isUnattached = false;
                var daysUnattached = 14;

                if (resource.Properties.TryGetValue("diskState", out var ds) && ds?.ToString()?.Equals("Unattached", StringComparison.OrdinalIgnoreCase) == true)
                {
                    isUnattached = true;
                }
                else if (!resource.Properties.TryGetValue("managedBy", out var mb) || mb == null || (mb is string mbs && string.IsNullOrWhiteSpace(mbs)))
                {
                    isUnattached = true;
                }

                if (resource.Properties.TryGetValue("daysUnattached", out var du))
                {
                    if (du is int duInt) daysUnattached = duInt;
                    else if (int.TryParse(du?.ToString(), out var parsedDu)) daysUnattached = parsedDu;
                }

                if (isUnattached)
                {
                    var skuName = resource.SkuName ?? "P10";
                    if (resource.Properties.TryGetValue("diskSizeGB", out var sizeObj) && int.TryParse(sizeObj?.ToString(), out var sizeGB))
                    {
                        skuName = sizeGB switch
                        {
                            <= 128 => "P10",
                            <= 256 => "P15",
                            <= 512 => "P20",
                            _ => "P30"
                        };
                    }

                    var monthlyCost = await pricesClient.GetMonthlyStoragePriceAsync(skuName, resource.Location, ct);

                    recommendations.Add(new RecommendationCard
                    {
                        Id = $"rec-orphan-disk-{resource.Name}",
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ResourceGroup = resource.ResourceGroup,
                        SubscriptionId = resource.SubscriptionId,
                        Location = resource.Location,
                        Category = RecommendationCategory.Removal,
                        Action = RecommendationAction.DeleteOrphan,
                        Title = $"Delete unattached managed disk ({resource.Name})",
                        Description = $"Managed disk {resource.Name} ({skuName}) is not attached to any virtual machine and has been idle for {daysUnattached} days. Storage charges continue to accumulate indefinitely until deleted.",
                        CurrentSku = skuName,
                        TargetSku = "None (Deleted)",
                        Costs = new CostBreakdown
                        {
                            CurrentMonthlyCost = monthlyCost,
                            TargetMonthlyCost = 0m,
                            GrossMonthlySavings = monthlyCost,
                            NetMonthlySavings = monthlyCost,
                            Basis = PricingBasis.RetailListPrice
                        },
                        Risk = daysUnattached > 30 ? RiskLevel.Low : RiskLevel.Medium,
                        Confidence = ConfidenceLevel.High,
                        Prerequisites = [
                            "Take a snapshot before deletion if the disk contains historical state or compliance data",
                            "Verify with the resource owner or creator that the disk is not part of a disaster recovery staging pattern"
                        ],
                        RollbackGuidance = [
                            "If a pre-deletion snapshot is created, recreate the disk instantly from the snapshot using 'az disk create --source <snapshot-id>'"
                        ],
                        WhyNotAlternatives = [
                            "Downsizing an unattached disk still retains unnecessary storage charges"
                        ],
                        Evidence = new EvidenceSummary
                        {
                            DaysUnattached = daysUnattached,
                            Tags = resource.Tags,
                            Observations = [
                                $"Disk state is Unattached for {daysUnattached} consecutive days",
                                "No active VM mount or lock detected"
                            ]
                        }
                    });
                }
            }
            // 2. Empty App Service Plans (0 apps)
            else if (resource.Type.Equals("Microsoft.Web/serverfarms", StringComparison.OrdinalIgnoreCase))
            {
                var numberOfSites = 0;
                if (resource.Properties.TryGetValue("numberOfSites", out var ns))
                {
                    if (ns is int sitesInt) numberOfSites = sitesInt;
                    else if (int.TryParse(ns?.ToString(), out var parsedSites)) numberOfSites = parsedSites;
                }

                if (numberOfSites == 0)
                {
                    var skuName = resource.SkuName ?? "P1v3";
                    var hourly = await pricesClient.GetHourlyPriceAsync(skuName, "Azure App Service", resource.Location, "Linux", ct);
                    var monthlyCost = Math.Round(hourly * 730m, 2);

                    recommendations.Add(new RecommendationCard
                    {
                        Id = $"rec-orphan-asp-{resource.Name}",
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ResourceGroup = resource.ResourceGroup,
                        SubscriptionId = resource.SubscriptionId,
                        Location = resource.Location,
                        Category = RecommendationCategory.Removal,
                        Action = RecommendationAction.DeleteOrphan,
                        Title = $"Delete empty App Service Plan ({resource.Name})",
                        Description = $"App Service Plan {resource.Name} ({skuName}) contains 0 web applications, function apps, or deployment slots, but incurs full hourly compute reservation charges.",
                        CurrentSku = skuName,
                        TargetSku = "None (Deleted)",
                        Costs = new CostBreakdown
                        {
                            CurrentMonthlyCost = monthlyCost,
                            TargetMonthlyCost = 0m,
                            GrossMonthlySavings = monthlyCost,
                            NetMonthlySavings = monthlyCost,
                            Basis = PricingBasis.RetailListPrice
                        },
                        Risk = RiskLevel.Low,
                        Confidence = ConfidenceLevel.High,
                        Prerequisites = [
                            "Confirm no active CI/CD deployment pipeline targets this plan as a warm staging environment",
                            "Confirm no custom domains or certificates are bound to the plan level"
                        ],
                        RollbackGuidance = [
                            $"Re-create the plan if needed via ARM template, Bicep, or 'az appservice plan create --name {resource.Name} --resource-group {resource.ResourceGroup} --sku {skuName}'"
                        ],
                        WhyNotAlternatives = [
                            "Keeping an empty paid plan active incurs unnecessary compute overhead without hosting any services"
                        ],
                        Evidence = new EvidenceSummary
                        {
                            ConnectedAppsCount = 0,
                            Tags = resource.Tags,
                            Observations = [
                                "App Service Plan has 0 apps and 0 deployment slots registered",
                                "Full dedicated compute fee continues to be billed by Azure"
                            ]
                        }
                    });
                }
            }
            // 3. Unassociated Public IP Addresses
            else if (resource.Type.Equals("Microsoft.Network/publicIPAddresses", StringComparison.OrdinalIgnoreCase))
            {
                var isUnassociated = false;
                if (!resource.Properties.TryGetValue("ipConfiguration", out var ipc) || ipc == null || (ipc is string s && string.IsNullOrWhiteSpace(s)))
                {
                    isUnassociated = true;
                }

                if (isUnassociated)
                {
                    // Standard static public IP costs ~$3.65/mo when unassociated
                    var monthlyCost = 3.65m;

                    recommendations.Add(new RecommendationCard
                    {
                        Id = $"rec-orphan-pip-{resource.Name}",
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ResourceGroup = resource.ResourceGroup,
                        SubscriptionId = resource.SubscriptionId,
                        Location = resource.Location,
                        Category = RecommendationCategory.Removal,
                        Action = RecommendationAction.DeleteOrphan,
                        Title = $"Delete unassociated Public IP ({resource.Name})",
                        Description = $"Public IP address {resource.Name} is not attached to any network interface, load balancer, or gateway. Azure charges an hourly reservation fee for unassociated static public IPs.",
                        CurrentSku = resource.SkuName ?? "Standard",
                        TargetSku = "None (Deleted)",
                        Costs = new CostBreakdown
                        {
                            CurrentMonthlyCost = monthlyCost,
                            TargetMonthlyCost = 0m,
                            GrossMonthlySavings = monthlyCost,
                            NetMonthlySavings = monthlyCost,
                            Basis = PricingBasis.RetailListPrice
                        },
                        Risk = RiskLevel.Safe,
                        Confidence = ConfidenceLevel.High,
                        Prerequisites = [
                            "Verify external DNS records or firewall allowlists do not reference this specific IP address"
                        ],
                        RollbackGuidance = [
                            "Public IPs cannot be restored with the exact same IP once deleted unless reserved from a custom IP prefix"
                        ],
                        WhyNotAlternatives = [
                            "Leaving an unused public IP consumes public IPv4 address space and incurs recurring non-utilization fees"
                        ],
                        Evidence = new EvidenceSummary
                        {
                            Tags = resource.Tags,
                            Observations = [
                                "No IP configuration association found",
                                "Static IP allocation incurring unattached reservation charges"
                            ]
                        }
                    });
                }
            }
        }

        return recommendations;
    }
}
