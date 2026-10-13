using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Providers;

public class LiveAzureDataProvider : IAzureDataProvider
{
    private readonly TokenCredential _credential;
    private readonly ArmClient _armClient;
    private readonly MetricsQueryClient _metricsClient;

    public AdvisorMode Mode => AdvisorMode.Live;

    public LiveAzureDataProvider(TokenCredential? credential = null)
    {
        // Try AzureCliCredential first for instant local developer experience, then DefaultAzureCredential
        _credential = credential ?? new ChainedTokenCredential(
            new AzureCliCredential(),
            new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true
            })
        );

        _armClient = new ArmClient(_credential);
        _metricsClient = new MetricsQueryClient(_credential);
    }

    public async Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        var subscriptions = new List<SubscriptionInfo>();

        try
        {
            // First try querying Azure CLI local account cache directly for instant response
            var cliSubs = await TryGetAzureCliSubscriptionsAsync(ct);
            if (cliSubs.Count > 0)
            {
                return cliSubs;
            }

            await foreach (var sub in _armClient.GetSubscriptions().GetAllAsync(ct))
            {
                subscriptions.Add(new SubscriptionInfo
                {
                    SubscriptionId = sub.Data.SubscriptionId,
                    DisplayName = sub.Data.DisplayName ?? sub.Data.SubscriptionId,
                    TenantId = sub.Data.TenantId?.ToString(),
                    State = sub.Data.State?.ToString() ?? "Enabled",
                    IsDemo = false
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Unable to list Azure subscriptions via DefaultAzureCredential. Ensure you have run 'az login' locally or have an active Managed Identity. Error: {ex.Message}", ex);
        }

        return subscriptions;
    }

    private static async Task<List<SubscriptionInfo>> TryGetAzureCliSubscriptionsAsync(CancellationToken ct)
    {
        var list = new List<SubscriptionInfo>();
        try
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "az",
                Arguments = "account list --output json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            proc.Start();
            var json = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        var id = elem.GetProperty("id").GetString();
                        var name = elem.GetProperty("name").GetString();
                        var tenantId = elem.TryGetProperty("tenantId", out var t) ? t.GetString() : null;
                        var state = elem.TryGetProperty("state", out var s) ? s.GetString() ?? "Enabled" : "Enabled";

                        if (!string.IsNullOrEmpty(id))
                        {
                            list.Add(new SubscriptionInfo
                            {
                                SubscriptionId = id,
                                DisplayName = name ?? id,
                                TenantId = tenantId,
                                State = state,
                                IsDemo = false
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently fall back to ARM SDK call
        }

        return list;
    }

    public async Task<IReadOnlyList<AzureResource>> GetResourcesAsync(string subscriptionId, CancellationToken ct = default)
    {
        var resources = new List<AzureResource>();

        var query = @"
            resources
            | where type in (
                'microsoft.compute/virtualmachines',
                'microsoft.web/serverfarms',
                'microsoft.compute/disks',
                'microsoft.network/publicipaddresses'
            )
            | project id, name, type, kind, location, resourceGroup, subscriptionId, sku, tags, properties
            | order by id asc
        ";

        try
        {
            string? skipToken = null;
            var tenantResource = _armClient.GetTenants().First();

            do
            {
                var queryContent = new ResourceQueryContent(query)
                {
                    Subscriptions = { subscriptionId },
                    Options = new ResourceQueryRequestOptions
                    {
                        SkipToken = skipToken,
                        Top = 1000
                    }
                };

                var response = await tenantResource.GetResourcesAsync(queryContent, ct);
                var rawData = response.Value.Data;

                if (rawData != null)
                {
                    using var doc = JsonDocument.Parse(rawData.ToString());
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var elem in doc.RootElement.EnumerateArray())
                        {
                            var id = elem.GetProperty("id").GetString() ?? "";
                            var name = elem.GetProperty("name").GetString() ?? "";
                            var type = elem.GetProperty("type").GetString() ?? "";
                            var kind = elem.TryGetProperty("kind", out var k) ? k.GetString() : null;
                            var location = elem.TryGetProperty("location", out var loc) ? loc.GetString() ?? "" : "";
                            var rg = elem.TryGetProperty("resourceGroup", out var r) ? r.GetString() ?? "" : "";

                            string? skuName = null;
                            string? skuTier = null;
                            if (elem.TryGetProperty("sku", out var skuObj) && skuObj.ValueKind == JsonValueKind.Object)
                            {
                                if (skuObj.TryGetProperty("name", out var sn)) skuName = sn.GetString();
                                if (skuObj.TryGetProperty("tier", out var st)) skuTier = st.GetString();
                            }

                            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            if (elem.TryGetProperty("tags", out var tagsObj) && tagsObj.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in tagsObj.EnumerateObject())
                                {
                                    tags[prop.Name] = prop.Value.GetString() ?? "";
                                }
                            }

                            var propsDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            if (elem.TryGetProperty("properties", out var propsObj) && propsObj.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in propsObj.EnumerateObject())
                                {
                                    propsDict[prop.Name] = ConvertJsonElement(prop.Value);
                                }
                            }

                            resources.Add(new AzureResource
                            {
                                Id = id,
                                Name = name,
                                Type = type,
                                Kind = kind,
                                Location = location,
                                ResourceGroup = rg,
                                SubscriptionId = subscriptionId,
                                SkuName = skuName,
                                SkuTier = skuTier,
                                Tags = tags,
                                Properties = propsDict
                            });
                        }
                    }
                }

                skipToken = response.Value.SkipToken;
            } while (!string.IsNullOrEmpty(skipToken));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to query Azure Resource Graph for subscription {subscriptionId}: {ex.Message}", ex);
        }

        return resources;
    }

    public async Task<IReadOnlyList<ResourceMetricSummary>> GetMetricsAsync(string subscriptionId, IEnumerable<string> resourceIds, CancellationToken ct = default)
    {
        var summaries = new List<ResourceMetricSummary>();
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-14);

        // Bounded concurrency metric querying
        foreach (var resourceId in resourceIds)
        {
            try
            {
                string[]? metricNames = null;
                if (resourceId.Contains("virtualMachines", StringComparison.OrdinalIgnoreCase))
                {
                    metricNames = ["Percentage CPU", "Available Memory Bytes"];
                }
                else if (resourceId.Contains("serverfarms", StringComparison.OrdinalIgnoreCase))
                {
                    metricNames = ["CpuPercentage", "MemoryPercentage"];
                }

                if (metricNames != null)
                {
                    var response = await _metricsClient.QueryResourceAsync(
                        resourceId,
                        metricNames,
                        new MetricsQueryOptions
                        {
                            TimeRange = new QueryTimeRange(start, now),
                            Granularity = TimeSpan.FromHours(1)
                        },
                        ct);

                    foreach (var metric in response.Value.Metrics)
                    {
                        var values = metric.TimeSeries
                            .SelectMany(ts => ts.Values)
                            .Where(v => v.Average.HasValue)
                            .Select(v => v.Average!.Value)
                            .ToList();

                        if (values.Count > 0)
                        {
                            values.Sort();
                            var p95Index = (int)Math.Ceiling(values.Count * 0.95) - 1;
                            p95Index = Math.Clamp(p95Index, 0, values.Count - 1);

                            summaries.Add(new ResourceMetricSummary
                            {
                                ResourceId = resourceId,
                                MetricName = metric.Name,
                                Average = values.Average(),
                                Maximum = values.Max(),
                                Percentile95 = values[p95Index],
                                SampleCount = values.Count,
                                WindowStartUtc = start.UtcDateTime,
                                WindowEndUtc = now.UtcDateTime
                            });
                        }
                    }
                }
            }
            catch
            {
                // Metric querying may be restricted or missing; keep collecting remaining resources
            }
        }

        return summaries;
    }

    public static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : (element.TryGetInt64(out var l) ? l : element.GetDouble()),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.Clone()
        };
    }
}
