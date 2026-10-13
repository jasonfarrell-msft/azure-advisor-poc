using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;

namespace AzureCostAdvisor.Core.Engine;

public class PortfolioConflictResolver : IPortfolioConflictResolver
{
    public IReadOnlyList<RecommendationCard> ResolveOverlaps(IEnumerable<RecommendationCard> recommendations)
    {
        var resolved = new List<RecommendationCard>();
        var groupedByResource = recommendations.GroupBy(r => r.ResourceId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groupedByResource)
        {
            var list = group.ToList();

            if (list.Count == 1)
            {
                resolved.Add(list[0]);
                continue;
            }

            // Priority: Removal (Delete) > Highest Net Savings
            // If there's a removal recommendation, it completely supersedes resizing/scheduling
            var removal = list.FirstOrDefault(r => r.Category == RecommendationCategory.Removal);
            if (removal != null)
            {
                resolved.Add(removal with { Status = RecommendationStatus.Actionable });

                foreach (var other in list.Where(r => r != removal))
                {
                    resolved.Add(other with
                    {
                        Status = RecommendationStatus.Overlapped,
                        Costs = other.Costs with
                        {
                            NetMonthlySavings = 0m,
                            ExclusionReason = $"Superseded by {removal.Title} ({removal.Action}) - resource is marked for deletion"
                        }
                    });
                }
                continue;
            }

            // Otherwise, sort by NetMonthlySavings descending
            var sorted = list.OrderByDescending(r => r.Costs.NetMonthlySavings).ToList();
            var winner = sorted[0];

            resolved.Add(winner with { Status = RecommendationStatus.Actionable });

            for (int i = 1; i < sorted.Count; i++)
            {
                var candidate = sorted[i];
                resolved.Add(candidate with
                {
                    Status = RecommendationStatus.Overlapped,
                    Costs = candidate.Costs with
                    {
                        NetMonthlySavings = 0m,
                        ExclusionReason = $"Overlapped with '{winner.Title}' to prevent double-counting portfolio savings on {winner.ResourceName}"
                    }
                });
            }
        }

        return resolved;
    }
}
