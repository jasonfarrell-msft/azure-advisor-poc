import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import {
  AdvisorCapabilities,
  AdvisorMode,
  SubscriptionInfo,
  AnalysisResult,
  RecommendationCard
} from './types';
import {
  fetchCapabilities,
  fetchMode,
  fetchSubscriptions,
  runAnalysisScan,
  explainRecommendation
} from './api';
import { Navbar } from './components/Navbar';
import { KpiSummary } from './components/KpiSummary';
import { RecommendationTable } from './components/RecommendationTable';
import { RecommendationDetailModal } from './components/RecommendationDetailModal';
import { SchedulePlannerModal } from './components/SchedulePlannerModal';
import { DataChatPanel } from './components/DataChatPanel';
import { Lightbulb, MessageSquareText, ShieldAlert, Sparkles } from 'lucide-react';

export const App: React.FC = () => {
  const [mode, setMode] = useState<AdvisorMode>('Demo');
  const [subscriptions, setSubscriptions] = useState<SubscriptionInfo[]>([]);
  const [selectedSubId, setSelectedSubId] = useState<string>('');
  const [analysis, setAnalysis] = useState<AnalysisResult | null>(null);
  const [isScanning, setIsScanning] = useState<boolean>(false);
  const [enrichWithAi, setEnrichWithAi] = useState<boolean>(false);
  const [capabilities, setCapabilities] = useState<AdvisorCapabilities>({
    foundryConfigured: false,
    foundryDeployment: '',
    foundryProject: '',
    demoScanDelaySeconds: 10,
    dataChatAvailable: false
  });
  const [selectedRec, setSelectedRec] = useState<RecommendationCard | null>(null);
  const [scheduleRec, setScheduleRec] = useState<RecommendationCard | null>(null);
  const [isChatOpen, setIsChatOpen] = useState(false);
  const [isExplaining, setIsExplaining] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const scanSequence = useRef(0);
  const selectionSequence = useRef(0);
  const initialized = useRef(false);

  const selectedSubscription = useMemo(
    () => subscriptions.find((subscription) => subscription.subscriptionId === selectedSubId) ?? null,
    [selectedSubId, subscriptions]
  );

  const loadSubscriptions = useCallback(async (
    currentMode: AdvisorMode,
    requestSequence: number,
    preferredSubscriptionId?: string
  ): Promise<string> => {
    const subs = await fetchSubscriptions(currentMode);
    if (requestSequence !== selectionSequence.current) return '';

    setSubscriptions(subs);

    const selectedId = subs.some((subscription) => subscription.subscriptionId === preferredSubscriptionId)
      ? preferredSubscriptionId!
      : subs[0]?.subscriptionId ?? '';

    setSelectedSubId(selectedId);
    return selectedId;
  }, []);

  const scanSubscription = useCallback(async (
    subscriptionId: string,
    scanMode: AdvisorMode
  ) => {
    if (!subscriptionId) return;

    const requestSequence = ++scanSequence.current;
    setAnalysis(null);
    setSelectedRec(null);
    setScheduleRec(null);
    setIsScanning(true);
    setErrorMessage(null);

    try {
      const result = await runAnalysisScan(subscriptionId, scanMode, enrichWithAi);
      if (requestSequence === scanSequence.current) {
        setAnalysis(result);
      }
    } catch (err: unknown) {
      if (requestSequence === scanSequence.current) {
        setErrorMessage(err instanceof Error ? err.message : 'Analysis scan failed');
      }
    } finally {
      if (requestSequence === scanSequence.current) {
        setIsScanning(false);
      }
    }
  }, [enrichWithAi]);

  useEffect(() => {
    if (initialized.current) return;
    initialized.current = true;

    const initialize = async () => {
      const requestSequence = ++selectionSequence.current;

      try {
        const [currentMode, advisorCapabilities] = await Promise.all([
          fetchMode().catch(() => 'Demo'),
          fetchCapabilities().catch(() => null)
        ]);
        if (requestSequence !== selectionSequence.current) return;

        if (advisorCapabilities) {
          setCapabilities(advisorCapabilities);
        }

        const parsedMode: AdvisorMode = currentMode === 'Live' ? 'Live' : 'Demo';
        setMode(parsedMode);
        const subscriptionId = await loadSubscriptions(parsedMode, requestSequence);
        if (requestSequence === selectionSequence.current && parsedMode === 'Demo') {
          await scanSubscription(subscriptionId, parsedMode);
        }
      } catch (err: unknown) {
        if (requestSequence === selectionSequence.current) {
          setErrorMessage(err instanceof Error ? err.message : 'Failed to initialize the advisor');
        }
      }
    };

    void initialize();
  }, [loadSubscriptions, scanSubscription]);

  const handleModeChange = async (newMode: AdvisorMode) => {
    if (newMode === mode) return;

    const requestSequence = ++selectionSequence.current;
    scanSequence.current += 1;
    setMode(newMode);
    setAnalysis(null);
    setSelectedRec(null);
    setScheduleRec(null);
    setIsScanning(false);
    setErrorMessage(null);

    try {
      const subscriptionId = await loadSubscriptions(newMode, requestSequence);
      if (requestSequence === selectionSequence.current && newMode === 'Demo') {
        await scanSubscription(subscriptionId, newMode);
      }
    } catch (err: unknown) {
      if (requestSequence === selectionSequence.current) {
        setErrorMessage(err instanceof Error ? err.message : 'Failed to switch mode');
      }
    }
  };

  const handleSubscriptionChange = (subscriptionId: string) => {
    selectionSequence.current += 1;
    setSelectedSubId(subscriptionId);
    if (mode === 'Demo') {
      void scanSubscription(subscriptionId, mode);
    } else {
      scanSequence.current += 1;
      setAnalysis(null);
      setSelectedRec(null);
      setScheduleRec(null);
      setIsScanning(false);
    }
  };

  const handleRunScan = () => {
    void scanSubscription(selectedSubId, mode);
  };

  // Explain on demand
  const handleExplainOnDemand = async (id: string) => {
    if (!analysis) return;

    const analysisJobId = analysis.jobId;
    setIsExplaining(true);
    try {
      const explanation = await explainRecommendation(id);
      if (explanation) {
        setAnalysis((currentAnalysis) => {
          if (!currentAnalysis || currentAnalysis.jobId !== analysisJobId) {
            return currentAnalysis;
          }

          return {
            ...currentAnalysis,
            recommendations: currentAnalysis.recommendations.map((recommendation) =>
              recommendation.id === id
                ? { ...recommendation, aiExplanation: explanation }
                : recommendation
            )
          };
        });
        setSelectedRec((currentRecommendation) =>
          currentRecommendation?.id === id
            ? { ...currentRecommendation, aiExplanation: explanation }
            : currentRecommendation
        );
      }
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Failed to generate AI explanation');
    } finally {
      setIsExplaining(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      {/* Top Navbar */}
      <Navbar
        mode={mode}
        onModeChange={handleModeChange}
        subscriptions={subscriptions}
        selectedSubscriptionId={selectedSubId}
        onSubscriptionChange={handleSubscriptionChange}
        onRunScan={handleRunScan}
        isScanning={isScanning}
        enrichWithAi={enrichWithAi}
        onEnrichWithAiChange={setEnrichWithAi}
        foundryConfigured={capabilities.foundryConfigured}
        foundryDeployment={capabilities.foundryDeployment}
      />

      {/* Main Container */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        <div
          role="status"
          className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-xs text-amber-900"
        >
          <strong>Pilot note:</strong> Scan results stay in this browser session. Server-side recommendation actions are isolated by authenticated operator and reset when the API restarts.
        </div>

        {mode === 'Demo' && selectedSubscription && (
          <section
            aria-live="polite"
            className="rounded-xl border border-indigo-200 bg-indigo-50 p-4 text-sm text-indigo-950 shadow-sm"
          >
            <div className="flex items-start gap-3">
              <Lightbulb className="mt-0.5 h-5 w-5 shrink-0 text-indigo-600" />
              <div>
                <p className="font-bold">
                  Demo scenario: {selectedSubscription.optimizationFocus ?? selectedSubscription.displayName}
                </p>
                <p className="mt-1 text-xs leading-5 text-indigo-800">
                  {selectedSubscription.scenarioSummary}
                </p>
                <p className="mt-2 text-xs font-medium text-indigo-700">
                  Selecting a demo subscription clears the previous results and runs a simulated {capabilities.demoScanDelaySeconds}-second discovery scan.
                </p>
              </div>
            </div>
          </section>
        )}

        {/* Error Notification Alert */}
        {errorMessage && (
          <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-start justify-between shadow-sm">
            <div className="flex items-start space-x-2.5">
              <ShieldAlert className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
              <div>
                <p className="font-bold">Error Notice</p>
                <p className="mt-0.5 text-red-700">{errorMessage}</p>
              </div>
            </div>
            <button
              type="button"
              onClick={() => setErrorMessage(null)}
              className="text-red-500 hover:text-red-800 font-bold ml-4"
            >
              Dismiss
            </button>
          </div>
        )}

        {/* Mode Information Callout */}
        <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-sm flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 text-xs">
          <div className="flex items-center space-x-2.5">
            <span className={`w-2.5 h-2.5 rounded-full ${mode === 'Live' ? 'bg-emerald-500 animate-pulse' : 'bg-indigo-500'}`} />
            <span className="text-slate-600">
              Active Environment: <strong className="text-slate-900">{mode === 'Live' ? 'Live Azure Tenant (Azure CLI / ARM)' : 'Synthetic Sandbox (Demo Dataset)'}</strong>
            </span>
          </div>
          <div className="flex items-center space-x-3 text-slate-500">
            <span>Read-Only Least Privilege</span>
            <span>•</span>
            <span>Deterministic FinOps Engine</span>
            <span>•</span>
            {capabilities.foundryConfigured ? (
              <span className="flex items-center text-indigo-600 font-medium">
                <Sparkles className="w-3.5 h-3.5 mr-1" />
                Azure AI Foundry enrichment configured
              </span>
            ) : (
              <span className="font-medium text-slate-600">Deterministic explanations available</span>
            )}
          </div>
        </div>

        {/* KPI Summary Cards */}
        <KpiSummary analysis={analysis} />

        {/* Recommendations Matrix */}
        {analysis && (
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-base font-bold text-slate-900">
                Actionable Cost Reduction Recommendations
              </h2>
              <span className="text-xs text-slate-500 font-medium">
                {analysis.recommendations.length} total candidates identified
              </span>
            </div>
            <RecommendationTable
              recommendations={analysis.recommendations}
              onSelectRecommendation={setSelectedRec}
              onOpenSchedulePlanner={setScheduleRec}
            />
          </div>
        )}
      </main>

      {/* Detail Modal */}
      <RecommendationDetailModal
        recommendation={selectedRec}
        onClose={() => setSelectedRec(null)}
        onExplainOnDemand={handleExplainOnDemand}
        isExplaining={isExplaining}
      />

      {/* Schedule Planner Simulator */}
      <SchedulePlannerModal
        recommendation={scheduleRec}
        onClose={() => setScheduleRec(null)}
      />

      <button
        type="button"
        onClick={() => setIsChatOpen(true)}
        className="fixed bottom-6 right-6 z-40 flex items-center gap-2 rounded-full bg-indigo-600 px-4 py-3 text-sm font-bold text-white shadow-xl shadow-indigo-600/25 transition hover:bg-indigo-500"
      >
        <MessageSquareText className="h-5 w-5" />
        Chat with advisor
      </button>

      <DataChatPanel
        isOpen={isChatOpen}
        onClose={() => setIsChatOpen(false)}
        analysis={analysis}
        subscription={selectedSubscription}
        foundryConfigured={capabilities.foundryConfigured}
      />

      {/* Footer */}
      <footer className="border-t border-slate-200 bg-white py-4 mt-auto">
        <div className="max-w-7xl mx-auto px-4 text-center text-xs text-slate-400">
          Azure Cost Advisor &copy; 2026 • {capabilities.foundryConfigured ? `Azure AI Foundry ${capabilities.foundryDeployment} configured` : 'Deterministic AI fallback'} • No credentials stored by the app
        </div>
      </footer>
    </div>
  );
};
