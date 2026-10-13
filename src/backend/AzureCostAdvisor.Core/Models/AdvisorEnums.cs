namespace AzureCostAdvisor.Core.Models;

public enum AdvisorMode
{
    Live,
    Demo
}

public enum RecommendationCategory
{
    Rightsizing,
    Scheduling,
    Removal
}

public enum RecommendationAction
{
    Resize,
    ScheduledShutdown,
    DeleteOrphan
}

public enum RiskLevel
{
    Safe,
    Low,
    Medium,
    High
}

public enum ConfidenceLevel
{
    Low,
    Medium,
    High
}

public enum PricingBasis
{
    RetailListPrice,
    CostManagementAmortized,
    CommitmentAdjusted
}

public enum RecommendationStatus
{
    Candidate,
    Actionable,
    ReviewRequired,
    Overlapped,
    Suppressed
}
