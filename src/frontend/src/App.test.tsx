import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import {
  explainRecommendation,
  fetchLatestAnalysis,
  fetchMode,
  fetchSubscriptions,
  runAnalysisScan
} from './api';
import { AnalysisResult, RecommendationCategory, SubscriptionInfo } from './types';

vi.mock('./api', () => ({
  explainRecommendation: vi.fn(),
  fetchLatestAnalysis: vi.fn(),
  fetchMode: vi.fn(),
  fetchSubscriptions: vi.fn(),
  runAnalysisScan: vi.fn()
}));

const subscriptions: SubscriptionInfo[] = [
  {
    subscriptionId: 'demo-production',
    displayName: 'Contoso Production (Demo)',
    state: 'Enabled',
    isDemo: true,
    optimizationFocus: 'Rightsizing',
    scenarioSummary: 'Find oversized production resources.'
  },
  {
    subscriptionId: 'demo-engineering',
    displayName: 'Contoso Engineering (Demo)',
    state: 'Enabled',
    isDemo: true,
    optimizationFocus: 'Scheduling',
    scenarioSummary: 'Deallocate non-production VMs outside business hours.'
  },
  {
    subscriptionId: 'demo-legacy',
    displayName: 'Contoso Legacy Estate (Demo)',
    state: 'Enabled',
    isDemo: true,
    optimizationFocus: 'Cleanup',
    scenarioSummary: 'Remove unattached and unused resources.'
  }
];

function createAnalysis(
  subscriptionId: string,
  resourceName: string,
  category: RecommendationCategory
): AnalysisResult {
  return {
    jobId: `job-${subscriptionId}`,
    subscriptionId,
    mode: 1,
    startedAtUtc: '2026-10-13T00:00:00Z',
    completedAtUtc: '2026-10-13T00:00:01Z',
    totalResourcesScanned: 1,
    totalGrossSavingsMonthly: 100,
    totalNetRealizableSavingsMonthly: 100,
    categorySavings: { [category]: 100 },
    categoryCounts: { [category]: 1 },
    riskCounts: { Low: 1 },
    recommendations: [
      {
        id: `rec-${resourceName}`,
        resourceId: `/subscriptions/${subscriptionId}/resources/${resourceName}`,
        resourceName,
        resourceType: 'Microsoft.Compute/virtualMachines',
        resourceGroup: 'rg-demo',
        subscriptionId,
        location: 'eastus',
        category,
        action: category === 'Scheduling' ? 'ScheduledShutdown' : category === 'Removal' ? 'DeleteOrphan' : 'Resize',
        title: `${category} ${resourceName}`,
        description: `Optimize ${resourceName}`,
        costs: {
          currentMonthlyCost: 200,
          targetMonthlyCost: 100,
          grossMonthlySavings: 100,
          netMonthlySavings: 100,
          retainedStorageCost: 0,
          retainedNetworkingCost: 0,
          currency: 'USD',
          basis: 'RetailListPrice'
        },
        risk: 'Low',
        confidence: 'High',
        status: 'Actionable',
        prerequisites: [],
        rollbackGuidance: [],
        whyNotAlternatives: [],
        evidence: {
          telemetryWindowDays: 14,
          tags: {},
          observations: []
        }
      }
    ]
  };
}

describe('demo subscription experience', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(fetchLatestAnalysis).mockResolvedValue(null);
    vi.mocked(fetchMode).mockResolvedValue('Demo');
    vi.mocked(fetchSubscriptions).mockResolvedValue(subscriptions);
    vi.mocked(explainRecommendation).mockResolvedValue(undefined);
  });

  it('shows all demo scenarios and automatically scans the first subscription', async () => {
    vi.mocked(runAnalysisScan).mockResolvedValue(
      createAnalysis('demo-production', 'vm-production-rightsize', 'Rightsizing')
    );

    render(<App />);

    expect(await screen.findByText('vm-production-rightsize')).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Contoso Production.*Rightsizing/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Contoso Engineering.*Scheduling/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Contoso Legacy Estate.*Cleanup/ })).toBeInTheDocument();
    expect(screen.getByText('Demo scenario: Rightsizing')).toBeInTheDocument();
    expect(runAnalysisScan).toHaveBeenCalledWith('demo-production', 'Demo', true);
  });

  it('runs a fresh first-scenario scan instead of restoring a shared demo result', async () => {
    vi.mocked(fetchLatestAnalysis).mockResolvedValue(
      createAnalysis('demo-legacy', 'stale-legacy-resource', 'Removal')
    );
    vi.mocked(runAnalysisScan).mockResolvedValue(
      createAnalysis('demo-production', 'fresh-production-resource', 'Rightsizing')
    );

    render(<App />);

    expect(await screen.findByText('fresh-production-resource')).toBeInTheDocument();
    expect(screen.queryByText('stale-legacy-resource')).not.toBeInTheDocument();
    expect(runAnalysisScan).toHaveBeenCalledWith('demo-production', 'Demo', true);
  });

  it('does not let initialization overwrite a newer switch to live mode', async () => {
    let resolveLatestAnalysis!: (analysis: AnalysisResult) => void;
    const latestAnalysis = new Promise<AnalysisResult>((resolve) => {
      resolveLatestAnalysis = resolve;
    });
    const liveSubscription: SubscriptionInfo = {
      subscriptionId: 'live-subscription',
      displayName: 'Contoso Live',
      state: 'Enabled',
      isDemo: false
    };

    vi.mocked(fetchLatestAnalysis).mockReturnValue(latestAnalysis);
    vi.mocked(fetchSubscriptions).mockImplementation(async (requestedMode) =>
      requestedMode === 'Live' ? [liveSubscription] : subscriptions
    );

    const user = userEvent.setup();
    render(<App />);
    await user.click(screen.getByRole('button', { name: /Live Azure/ }));
    expect(await screen.findByRole('option', { name: 'Contoso Live' })).toBeInTheDocument();

    await act(async () => {
      resolveLatestAnalysis(createAnalysis('demo-legacy', 'stale-legacy-resource', 'Removal'));
      await latestAnalysis;
    });

    expect(screen.getByText('Live Azure Tenant (Azure CLI / ARM)')).toBeInTheDocument();
    expect(screen.queryByText('stale-legacy-resource')).not.toBeInTheDocument();
    expect(runAnalysisScan).not.toHaveBeenCalled();
  });

  it('clears the previous scan immediately and loads recommendations for the selected demo subscription', async () => {
    let resolveEngineeringScan!: (analysis: AnalysisResult) => void;
    const engineeringScan = new Promise<AnalysisResult>((resolve) => {
      resolveEngineeringScan = resolve;
    });
    vi.mocked(runAnalysisScan)
      .mockResolvedValueOnce(createAnalysis('demo-production', 'vm-production-rightsize', 'Rightsizing'))
      .mockReturnValueOnce(engineeringScan);

    const user = userEvent.setup();
    render(<App />);
    expect(await screen.findByText('vm-production-rightsize')).toBeInTheDocument();

    await user.selectOptions(screen.getByRole('combobox', { name: 'Azure subscription' }), 'demo-engineering');

    expect(screen.queryByText('vm-production-rightsize')).not.toBeInTheDocument();
    expect(screen.getByText('Demo scenario: Scheduling')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Scanning/ })).toBeDisabled();

    await act(async () => {
      resolveEngineeringScan(
        createAnalysis('demo-engineering', 'vm-engineering-schedule', 'Scheduling')
      );
      await engineeringScan;
    });

    expect(await screen.findByText('vm-engineering-schedule')).toBeInTheDocument();
    expect(runAnalysisScan).toHaveBeenLastCalledWith('demo-engineering', 'Demo', true);
  });

  it('ignores an older scan response after the user selects a newer demo subscription', async () => {
    let resolveEngineeringScan!: (analysis: AnalysisResult) => void;
    let resolveLegacyScan!: (analysis: AnalysisResult) => void;
    const engineeringScan = new Promise<AnalysisResult>((resolve) => {
      resolveEngineeringScan = resolve;
    });
    const legacyScan = new Promise<AnalysisResult>((resolve) => {
      resolveLegacyScan = resolve;
    });

    vi.mocked(runAnalysisScan)
      .mockResolvedValueOnce(createAnalysis('demo-production', 'vm-production-rightsize', 'Rightsizing'))
      .mockReturnValueOnce(engineeringScan)
      .mockReturnValueOnce(legacyScan);

    const user = userEvent.setup();
    render(<App />);
    expect(await screen.findByText('vm-production-rightsize')).toBeInTheDocument();

    const subscriptionSelector = screen.getByRole('combobox', { name: 'Azure subscription' });
    await user.selectOptions(subscriptionSelector, 'demo-engineering');
    await user.selectOptions(subscriptionSelector, 'demo-legacy');

    await act(async () => {
      resolveLegacyScan(createAnalysis('demo-legacy', 'disk-legacy-cleanup', 'Removal'));
      await legacyScan;
    });
    expect(await screen.findByText('disk-legacy-cleanup')).toBeInTheDocument();

    await act(async () => {
      resolveEngineeringScan(
        createAnalysis('demo-engineering', 'vm-engineering-schedule', 'Scheduling')
      );
      await engineeringScan;
    });

    expect(screen.getByText('disk-legacy-cleanup')).toBeInTheDocument();
    expect(screen.queryByText('vm-engineering-schedule')).not.toBeInTheDocument();
  });
});
