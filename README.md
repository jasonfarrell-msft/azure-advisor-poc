# Azure Cost Advisor (FinOps Advisor)

Azure Cost Advisor is a workload-aware FinOps application that analyzes Azure infrastructure deployments, resource telemetry, and configurations to generate evidence-backed, high-confidence cost reduction recommendations.

It combines a deterministic calculation engine with optional Azure AI Foundry enrichment for executive summaries, tradeoff analysis, and step-by-step implementation and rollback guidance.

---

## Key Features

1. **Dual-Mode Operation**:
   - **Demo Mode**: Guided analysis across three synthetic subscriptions, each designed to highlight a different optimization strategy:
     - **Contoso Production**: a stable, continuously running production VM evaluated for a 1-year Reserved Instance plus underutilized virtual machines and an App Service plan evaluated for rightsizing.
     - **Contoso Engineering**: non-production virtual machines that produce automated scheduling recommendations.
     - **Contoso Legacy Estate**: unattached disks, an empty App Service plan, and an unassociated public IP that produce cleanup recommendations.
     - The dashboard labels each subscription with its optimization focus, explains the selected scenario, and automatically clears and refreshes recommendations when a different demo subscription is selected.
     - Demo scans intentionally take 10 seconds to simulate Azure resource and telemetry discovery.
   - **Live Azure Mode**: Discovers tenant subscriptions accessible to the local user via Azure CLI (`az account list`) and ARM credentials. Ingests assets using Azure Resource Graph and Azure Monitor Metrics.

2. **Deterministic FinOps Engines**:
   - **Reservation Detector**: Identifies stable, continuously running Linux production virtual machines with a complete 14-day hourly telemetry baseline, verifies an eligible 1-year Reserved Instance price through the Azure Retail Prices API, and calculates potential commitment savings. Reservations remain **Review Required** and are excluded from realizable savings until a 30-day demand, scope, and existing-reservation/savings-plan coverage review is complete.
   - **Rightsizing Detector**: Identifies sustained underutilized virtual machines and App Service Plans based on 14-day P95 CPU and memory percentiles, mapping to optimal lower-cost SKUs without risking capacity degradation.
   - **Scheduling Detector**: Identifies non-production 24/7 workloads and calculates automated shutdown/restart schedules (e.g., M-F 07:00-19:00 UTC). Accurately accounts for avoided compute spend while factoring in retained storage drag (OS/data disks continue to accrue storage charges during compute deallocation).
   - **Orphan Resource Detector**: Finds unattached managed disks (idle >14 days), empty App Service Plans (0 registered apps or deployment slots), and unassociated static Public IPs.
   - **Portfolio Conflict Resolver**: Eliminates portfolio double-counting when multiple actions apply to the same resource (e.g., rightsizing vs. scheduling). The higher-savings action is marked Actionable, and the overlapping action is tagged as Overlapped with zero net savings.

### Questions the advisor answers

- **Should I move assets to Reserved Instances?** The Reservation detector surfaces eligible, stable Linux production VMs only when Azure publishes a lower 1-year reservation price. It shows potential savings and the scope, flexibility, existing-commitment, and 30-day validation checks required before purchase; potential commitment savings are not included in realizable totals until those checks are complete.
- **Should I downsize assets based on usage?** The Rightsizing detector uses measured 14-day CPU and memory telemetry, requires adequate headroom, and compares current and target SKU prices.
- **How can I save money?** The dashboard combines commitment discounts, rightsizing, non-production scheduling, and orphan cleanup into gross and conflict-adjusted monthly and annual savings.

3. **Interactive Schedule Simulator**:
   - Real-time what-if analysis tool allowing engineers to adjust startup/shutdown hours and weekend policies with live recalculated avoided compute spend and retained storage drag.

4. **Azure AI Foundry Enrichment**:
   - Optionally integrates with an existing Azure AI Foundry account and model deployment.
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

### Deploy to your Azure subscription

The deployment script provisions a dedicated resource group, virtual network, private ACR endpoint, Premium Azure Container Registry, Container Apps environment and API, Standard Static Web App, managed identity, RBAC, linked backend, and Easy Auth. Resource names are generated from the subscription ID and environment name, so the same command can be used in different subscriptions.

> **Cost note:** Premium ACR, Standard Static Web Apps, Container Apps, private networking, and optional Foundry model usage incur Azure charges. Review the Provider what-if printed by the script before it proceeds.

Prerequisites:

- Azure CLI signed in to the target tenant and subscription.
- Permission to create subscription deployments, resource groups, resources, and role assignments.
- Bash, `jq`, .NET 10, Node.js 20+, and npm.

```bash
az login
./scripts/deploy.sh \
  --subscription "<subscription-id>" \
  --environment "azureadvisor-dev" \
  --admin-email "you@contoso.com"
```

The script runs tests and a Provider-level what-if, creates bootstrap infrastructure, builds the backend in ACR, redeploys using an immutable image digest, disables ACR public access, deploys the frontend, verifies backend authentication, and prints a seven-day `operator` invitation for the administrator.

During the ACR build, public network access is temporarily restricted to the deployer's detected public IPv4 address. Use `--bootstrap-cidr` when deploying behind a NAT gateway, proxy, or controlled egress range. The failure trap uses deterministic resource names to disable ACR public access even if bootstrap deployment fails after creating the registry.

Live Azure scanning requires subscription Reader for the application identity and is intentionally opt-in:

```bash
./scripts/deploy.sh \
  --subscription "<subscription-id>" \
  --environment "azureadvisor-prod" \
  --admin-email "you@contoso.com" \
  --enable-live-reader
```

Redeploying without `--enable-live-reader` removes the dedicated application identity's subscription-scope Reader assignment. Likewise, omitting Foundry parameters removes stale **Cognitive Services OpenAI User** assignments from that dedicated identity.

Azure AI Foundry is optional. Without these parameters, deterministic explanations remain available:

```bash
./scripts/deploy.sh \
  --subscription "<subscription-id>" \
  --environment "azureadvisor-prod" \
  --foundry-resource-group "<foundry-resource-group>" \
  --foundry-account "<foundry-account-name>" \
  --foundry-endpoint "https://<foundry-account>.openai.azure.com/" \
  --foundry-deployment "gpt-5.4-mini" \
  --foundry-project "<project-name>"
```

The script grants only **Cognitive Services OpenAI User** to the application identity at the specified Foundry account scope. It does not store keys, deployment tokens, or connection strings.

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
