# Azure Cost Advisor (FinOps Advisor)

Azure Cost Advisor is a workload-aware FinOps application that analyzes Azure infrastructure deployments, resource telemetry, and configurations to generate evidence-backed, high-confidence cost reduction recommendations.

It combines a deterministic calculation engine with Azure AI Foundry (`gpt-5.4-mini`) for executive summaries, tradeoff analysis, and step-by-step implementation and rollback guidance.

---

## Key Features

1. **Dual-Mode Operation**:
   - **Demo Mode**: Guided analysis across three synthetic subscriptions, each designed to highlight a different optimization strategy:
     - **Contoso Production**: underutilized virtual machines and App Service plans that produce rightsizing recommendations.
     - **Contoso Engineering**: non-production virtual machines that produce automated scheduling recommendations.
     - **Contoso Legacy Estate**: unattached disks, an empty App Service plan, and an unassociated public IP that produce cleanup recommendations.
     - The dashboard labels each subscription with its optimization focus, explains the selected scenario, and automatically clears and refreshes recommendations when a different demo subscription is selected.
     - Demo scans intentionally take 10 seconds to simulate Azure resource and telemetry discovery.
   - **Live Azure Mode**: Discovers tenant subscriptions accessible to the local user via Azure CLI (`az account list`) and ARM credentials. Ingests assets using Azure Resource Graph and Azure Monitor Metrics.

2. **Deterministic FinOps Engines**:
   - **Rightsizing Detector**: Identifies sustained underutilized virtual machines and App Service Plans based on 14-day P95 CPU and memory percentiles, mapping to optimal lower-cost SKUs without risking capacity degradation.
   - **Scheduling Detector**: Identifies non-production 24/7 workloads and calculates automated shutdown/restart schedules (e.g., M-F 07:00-19:00 UTC). Accurately accounts for avoided compute spend while factoring in retained storage drag (OS/data disks continue to accrue storage charges during compute deallocation).
   - **Orphan Resource Detector**: Finds unattached managed disks (idle >14 days), empty App Service Plans (0 registered apps or deployment slots), and unassociated static Public IPs.
   - **Portfolio Conflict Resolver**: Eliminates portfolio double-counting when multiple actions apply to the same resource (e.g., rightsizing vs. scheduling). The higher-savings action is marked Actionable, and the overlapping action is tagged as Overlapped with zero net savings.

3. **Interactive Schedule Simulator**:
   - Real-time what-if analysis tool allowing engineers to adjust startup/shutdown hours and weekend policies with live recalculated avoided compute spend and retained storage drag.

4. **Azure AI Foundry Enrichment**:
   - Integrates with Azure AI Foundry account `foundry-sandbox-swc-mx01` and project `finops-advisor-project` using `gpt-5.4-mini`.
   - Uses the Container App's user-assigned managed identity with the least-privilege **Cognitive Services OpenAI User** role; no API keys are stored.
   - Strictly enforces prompt injection defenses: freeform resource names and tags are omitted; only sanitized metrics and financial baselines are passed to the model.
   - Seamless deterministic fallback if AI service is unreachable or rate-limited.

5. **Current-Data Chat Preview**:
   - A launchable right-side panel carries the active subscription, resource count, recommendation count, and savings context.
   - The current implementation is an explicit preview shell; it does not submit conversational prompts to a backend chat endpoint yet.

6. **Operator Isolation and Backend Authentication**:
   - Azure Static Web Apps authentication is enforced on the linked Container App backend and persisted in Bicep.
   - Advisor mode is client-selected rather than process-global, and server-side recommendation actions resolve against the authenticated operator's latest scan instead of a shared global result.

---

## Tech Stack

- **Backend**: .NET 10 (C# 14), ASP.NET Core Minimal APIs
  - Azure SDKs: `Azure.Identity`, `Azure.ResourceManager`, `Azure.ResourceManager.ResourceGraph`, `Azure.Monitor.Query`, `Azure.AI.OpenAI`
  - Unit Testing: `xUnit`, `coverlet`
- **Frontend**: React 19, TypeScript, Vite, Tailwind CSS, Lucide Icons

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js](https://nodejs.org/) (v20+) and `npm`
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (optional, required for Live Azure mode)

### 1. Run the Backend API

```bash
cd src/backend/AzureCostAdvisor.Api
dotnet run
```
The API server starts on `http://localhost:5000` (and `https://localhost:5001`).

### 2. Run the Frontend Dashboard

```bash
cd src/frontend
npm install
npm run dev
```
Open `http://localhost:5173` in your browser.

---

## Running the Unit Tests

The backend test suite validates portfolio conflict resolution (double-counting prevention), threshold rightsizing, retained storage drag, demo subscription scenarios, and Resource Graph property deserialization:

```bash
dotnet test AzureCostAdvisor.slnx
```

The frontend test suite validates demo scenario discovery, automatic initial loading, clearing previous results when a scan starts, and protection against stale scan responses:

```bash
npm test --prefix src/frontend
```
