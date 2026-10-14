using AzureCostAdvisor.Core.AI;
using AzureCostAdvisor.Core.Engine;
using AzureCostAdvisor.Core.Interfaces;
using AzureCostAdvisor.Core.Models;
using AzureCostAdvisor.Core.Providers;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<IRetailPricesClient, RetailPricesClient>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Ingestion Providers
builder.Services.AddSingleton<SyntheticDemoDataProvider>();
builder.Services.AddSingleton<LiveAzureDataProvider>();

// FinOps Detectors
builder.Services.AddSingleton<IFinOpsDetector, RightsizingDetector>();
builder.Services.AddSingleton<IFinOpsDetector, ReservationDetector>();
builder.Services.AddSingleton<IFinOpsDetector, SchedulingDetector>();
builder.Services.AddSingleton<IFinOpsDetector, OrphanResourceDetector>();

// Conflict Resolver & Orchestrator
builder.Services.AddSingleton<IPortfolioConflictResolver, PortfolioConflictResolver>();

// Azure AI Foundry Configuration
var foundryConfig = builder.Configuration.GetSection("AzureAiFoundry");
var endpoint = foundryConfig["Endpoint"];
var deployment = string.IsNullOrWhiteSpace(foundryConfig["DeploymentName"])
    ? "gpt-5.4-mini"
    : foundryConfig["DeploymentName"];
var project = foundryConfig["ProjectName"];

builder.Services.AddSingleton<IAiContextExplainer>(sp =>
    new AiContextExplainer(endpoint, deployment, project));

builder.Services.AddSingleton<CostAdvisorOrchestrator>();

// CORS origins are configured per environment; production should use the exact HTTPS frontend origin.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (builder.Environment.IsDevelopment())
        {
            allowedOrigins = allowedOrigins
                .Concat(["http://localhost:3000", "http://localhost:5173", "http://127.0.0.1:3000"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowLocalFrontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Endpoints

// 1. Mode Switcher
app.MapGet("/api/settings/mode", () => Results.Ok(new { mode = AdvisorMode.Demo.ToString() }));

app.MapGet("/api/settings/capabilities", (IAiContextExplainer aiExplainer) => Results.Ok(new
{
    foundryConfigured = aiExplainer.IsConfigured,
    foundryDeployment = aiExplainer.DeploymentName,
    foundryProject = aiExplainer.ProjectName,
    demoScanDelaySeconds = (int)SyntheticDemoDataProvider.SimulatedScanDuration.TotalSeconds,
    dataChatAvailable = false
}));

app.MapPost("/api/settings/mode", ([FromBody] ModeRequest request) =>
{
    if (Enum.TryParse<AdvisorMode>(request.Mode, true, out var parsed))
    {
        return Results.Ok(new
        {
            mode = parsed.ToString(),
            message = $"Advisor mode set to {parsed} for this client."
        });
    }
    return Results.BadRequest(new { error = "Invalid mode. Allowed values: Live, Demo" });
});

// 2. Subscriptions
app.MapGet("/api/subscriptions", async (
    [FromQuery] string? mode,
    SyntheticDemoDataProvider demoProvider,
    LiveAzureDataProvider liveProvider,
    CancellationToken ct) =>
{
    var effectiveMode = AdvisorMode.Demo;
    if (!string.IsNullOrEmpty(mode) && Enum.TryParse<AdvisorMode>(mode, true, out var queryMode))
    {
        effectiveMode = queryMode;
    }

    try
    {
        if (effectiveMode == AdvisorMode.Live)
        {
            var liveSubs = await liveProvider.GetSubscriptionsAsync(ct);
            return Results.Ok(new { mode = "Live", subscriptions = liveSubs });
        }
        else
        {
            var demoSubs = await demoProvider.GetSubscriptionsAsync(ct);
            return Results.Ok(new { mode = "Demo", subscriptions = demoSubs });
        }
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Failed to retrieve subscriptions",
            detail: ex.Message,
            statusCode: 500);
    }
});

// 3. Scan & Analyze Subscription
app.MapPost("/api/analysis/scan", async (
    [FromBody] ScanRequest request,
    CostAdvisorOrchestrator orchestrator,
    SyntheticDemoDataProvider demoProvider,
    LiveAzureDataProvider liveProvider,
    HttpContext context,
    CancellationToken ct) =>
{
    var mode = AdvisorMode.Demo;
    if (!string.IsNullOrEmpty(request.Mode) && Enum.TryParse<AdvisorMode>(request.Mode, true, out var reqMode))
    {
        mode = reqMode;
    }

    IAzureDataProvider provider = mode == AdvisorMode.Live ? liveProvider : demoProvider;

    try
    {
        if (mode == AdvisorMode.Demo)
        {
            await demoProvider.SimulateScanAsync(ct);
        }

        var result = await orchestrator.RunAnalysisAsync(
            provider,
            request.SubscriptionId,
            enrichWithAi: request.EnrichWithAi,
            analysisScope: GetAnalysisScope(context),
            ct: ct);

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Analysis Scan Failed",
            detail: ex.Message,
            statusCode: 500);
    }
});

// 4. Get Latest Analysis Result for the authenticated operator
app.MapGet("/api/analysis/latest", (CostAdvisorOrchestrator orchestrator, HttpContext context) =>
{
    var latest = orchestrator.GetLatestAnalysis(GetAnalysisScope(context));
    return latest != null ? Results.Ok(latest) : Results.NotFound(new { message = "No analysis has been run yet." });
});

// 5. On-Demand AI Explanation
app.MapPost("/api/recommendations/{id}/explain", async (
    string id,
    CostAdvisorOrchestrator orchestrator,
    HttpContext context,
    CancellationToken ct) =>
{
    var latest = orchestrator.GetLatestAnalysis(GetAnalysisScope(context));
    if (latest == null)
    {
        return Results.NotFound(new { error = "No analysis results found" });
    }

    var card = latest.Recommendations.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    if (card == null)
    {
        return Results.NotFound(new { error = $"Recommendation '{id}' not found" });
    }

    var explanation = await orchestrator.ExplainRecommendationAsync(card, ct);
    return Results.Ok(explanation);
});

// 6. Recalculate Custom Schedule Savings
app.MapPost("/api/schedules/calculate", (
    [FromBody] ScheduleCalculationRequest request,
    CostAdvisorOrchestrator orchestrator,
    HttpContext context) =>
{
    var latest = orchestrator.GetLatestAnalysis(GetAnalysisScope(context));
    if (latest == null)
    {
        return Results.NotFound(new { error = "No active analysis result found" });
    }

    var card = latest.Recommendations.FirstOrDefault(r => r.Id.Equals(request.RecommendationId, StringComparison.OrdinalIgnoreCase));
    if (card == null)
    {
        return Results.NotFound(new { error = $"Recommendation '{request.RecommendationId}' not found" });
    }

    var scheduleResult = orchestrator.CalculateCustomSchedule(
        card,
        request.StartHour,
        request.StopHour,
        request.IncludeWeekends);

    return Results.Ok(scheduleResult);
});

app.Run();

static string GetAnalysisScope(HttpContext context)
{
    var staticWebAppPrincipalId = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(staticWebAppPrincipalId))
    {
        return staticWebAppPrincipalId;
    }

    var authenticatedPrincipalId = context.User.Claims
        .FirstOrDefault(claim => claim.Type is "sub" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
        ?.Value;

    return string.IsNullOrWhiteSpace(authenticatedPrincipalId)
        ? "local-development"
        : authenticatedPrincipalId;
}

public record ModeRequest(string Mode);
public record ScanRequest(string SubscriptionId, string? Mode = null, bool EnrichWithAi = true);
