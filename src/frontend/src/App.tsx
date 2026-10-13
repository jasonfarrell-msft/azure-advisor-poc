import React, { useState, useEffect, useCallback } from 'react';
import { AdvisorMode, SubscriptionInfo, AnalysisResult, RecommendationCard } from './types';
import { fetchMode, fetchSubscriptions, runAnalysisScan, fetchLatestAnalysis, explainRecommendation } from './api';
import { Navbar } from './components/Navbar';
import { KpiSummary } from './components/KpiSummary';
import { RecommendationTable } from './components/RecommendationTable';
import { RecommendationDetailModal } from './components/RecommendationDetailModal';
import { SchedulePlannerModal } from './components/SchedulePlannerModal';
import { ShieldAlert, Sparkles } from 'lucide-react';

export const App: React.FC = () => {
  const [mode, setMode] = useState<AdvisorMode>('Demo');
  const [subscriptions, setSubscriptions] = useState<SubscriptionInfo[]>([]);
  const [selectedSubId, setSelectedSubId] = useState<string>('');
  const [analysis, setAnalysis] = useState<AnalysisResult | null>(null);
  const [isScanning, setIsScanning] = useState<boolean>(false);
  const [enrichWithAi, setEnrichWithAi] = useState<boolean>(() => !import.meta.env.PROD);
  const [selectedRec, setSelectedRec] = useState<RecommendationCard | null>(null);
  const [scheduleRec, setScheduleRec] = useState<RecommendationCard | null>(null);
  const [isExplaining, setIsExplaining] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Initialize mode and subscriptions
  const loadSubscriptions = useCallback(async (currentM: AdvisorMode) => {
    try {
      setErrorMessage(null);
      const subs = await fetchSubscriptions(currentM);
      setSubscriptions(subs);
      if (subs.length > 0) {
        setSelectedSubId(subs[0].subscriptionId);
      } else {
        setSelectedSubId('');
      }
    } catch (err: any) {
      setErrorMessage(err.message || 'Failed to load subscriptions');
    }
  }, []);

  useEffect(() => {
    const initialize = async () => {
      const latest = await fetchLatestAnalysis().catch(() => null);
      if (latest) {
        const latestMode: AdvisorMode = latest.mode === 0 ? 'Live' : 'Demo';
        setAnalysis(latest);
        setMode(latestMode);
        await loadSubscriptions(latestMode);
        return;
      }

      const currentMode = await fetchMode().catch(() => 'Demo');
      const parsedMode: AdvisorMode = currentMode === 'Live' ? 'Live' : 'Demo';
      setMode(parsedMode);
      await loadSubscriptions(parsedMode);
    };

    void initialize();
  }, [loadSubscriptions]);

  // Mode change handler
  const handleModeChange = async (newMode: AdvisorMode) => {
    setMode(newMode);
    try {
      await loadSubscriptions(newMode);
    } catch (err: any) {
      setErrorMessage(err.message || 'Failed to switch mode');
    }
  };

  // Run scan handler
  const handleRunScan = async () => {
    if (!selectedSubId) return;
    setIsScanning(true);
    setErrorMessage(null);

    try {
      const res = await runAnalysisScan(selectedSubId, mode, enrichWithAi);
      setAnalysis(res);
    } catch (err: any) {
      setErrorMessage(err.message || 'Analysis scan failed');
    } finally {
      setIsScanning(false);
    }
  };

  // Explain on demand
  const handleExplainOnDemand = async (id: string) => {
    setIsExplaining(true);
    try {
      const explanation = await explainRecommendation(id);
      if (explanation && analysis) {
        const updatedRecs = analysis.recommendations.map((r) =>
          r.id === id ? { ...r, aiExplanation: explanation } : r
        );
        const updatedAnalysis = { ...analysis, recommendations: updatedRecs };
        setAnalysis(updatedAnalysis);
        if (selectedRec && selectedRec.id === id) {
          setSelectedRec({ ...selectedRec, aiExplanation: explanation });
        }
      }
    } catch (err: any) {
      setErrorMessage(err.message || 'Failed to generate AI explanation');
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
        onSubscriptionChange={setSelectedSubId}
        onRunScan={handleRunScan}
        isScanning={isScanning}
        enrichWithAi={enrichWithAi}
        onEnrichWithAiChange={setEnrichWithAi}
        hostedFoundryEnabled={!import.meta.env.PROD}
      />

      {/* Main Container */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        <div
          role="status"
          className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-xs text-amber-900"
        >
          <strong>Pilot limitation:</strong> This deployment has one shared in-memory session. Mode and latest analysis are shared across operator tabs and reset when the API restarts.
        </div>

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
            {import.meta.env.PROD ? (
              <span className="font-medium text-slate-600">Hosted Foundry disabled; deterministic explanations</span>
            ) : (
              <span className="flex items-center text-indigo-600 font-medium">
                <Sparkles className="w-3.5 h-3.5 mr-1" />
                Local Foundry integration
              </span>
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

      {/* Footer */}
      <footer className="border-t border-slate-200 bg-white py-4 mt-auto">
        <div className="max-w-7xl mx-auto px-4 text-center text-xs text-slate-400">
          Azure Cost Advisor &copy; 2026 • {import.meta.env.PROD ? 'Hosted Foundry disabled in this Central US pilot' : 'Local Foundry integration may be enabled'} • No credentials stored by the app
        </div>
      </footer>
    </div>
  );
};
