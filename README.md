# Azure Cost Advisor (FinOps Advisor)

Azure Cost Advisor is a workload-aware FinOps application that analyzes Azure infrastructure deployments, resource telemetry, and configurations to generate evidence-backed, high-confidence cost reduction recommendations.

It combines a deterministic calculation engine with Azure AI Foundry (`gpt-5.4-mini`) for executive summaries, tradeoff analysis, and step-by-step implementation and rollback guidance.

---

## Key Features

1. **Dual-Mode Operation**:
   - **Demo Mode**: Instant analysis across three synthetic subscriptions, each designed to highlight a different optimization strategy:
     - **Contoso Production**: underutilized virtual machines and App Service plans that produce rightsizing recommendations.
     - **Contoso Engineering**: non-production virtual machines that produce automated scheduling recommendations.
     - **Contoso Legacy Estate**: unattached disks, an empty App Service plan, and an unassociated public IP that produce cleanup recommendations.
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
   - Strictly enforces prompt injection defenses: freeform resource names and tags are omitted; only sanitized metrics and financial baselines are passed to the model.
   - Seamless deterministic fallback if AI service is unreachable or rate-limited.

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

The test suite validates portfolio conflict resolution (double-counting prevention), threshold rightsizing, retained storage drag, and Resource Graph property deserialization:

```bash
dotnet test AzureCostAdvisor.slnx
```
