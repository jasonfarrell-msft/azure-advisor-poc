import React, { useState } from 'react';
import { RecommendationCard } from '../types';
import { X, Sparkles, AlertTriangle, RotateCcw, CheckSquare } from 'lucide-react';

interface RecommendationDetailModalProps {
  recommendation: RecommendationCard | null;
  onClose: () => void;
  onExplainOnDemand: (id: string) => Promise<void>;
  isExplaining: boolean;
}

export const RecommendationDetailModal: React.FC<RecommendationDetailModalProps> = ({
  recommendation,
  onClose,
  onExplainOnDemand,
  isExplaining
}) => {
  const [activeTab, setActiveTab] = useState<'overview' | 'evidence' | 'ai'>('overview');

  if (!recommendation) return null;

  const { costs, evidence, aiExplanation } = recommendation;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 overflow-y-auto">
      <div className="bg-white rounded-2xl shadow-2xl border border-slate-200 w-full max-w-4xl max-h-[90vh] flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-200 bg-slate-50 flex items-center justify-between">
          <div>
            <div className="flex items-center space-x-2">
              <span className="text-xs px-2.5 py-0.5 rounded-full font-semibold bg-blue-100 text-blue-800 uppercase tracking-wide">
                {recommendation.category}
              </span>
              <span className="text-xs text-slate-500 font-mono">
                {recommendation.resourceType}
              </span>
            </div>
            <h2 className="text-lg font-bold text-slate-900 mt-1">{recommendation.title}</h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 hover:bg-slate-200/60 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tabs Bar */}
        <div className="px-6 border-b border-slate-200 bg-white flex space-x-6 text-xs font-semibold">
          <button
            type="button"
            onClick={() => setActiveTab('overview')}
            className={`py-3 border-b-2 transition-all ${
              activeTab === 'overview'
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            Cost & Strategy Overview
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('evidence')}
            className={`py-3 border-b-2 transition-all ${
              activeTab === 'evidence'
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            Telemetry & Evidence
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('ai')}
            className={`py-3 border-b-2 flex items-center space-x-1.5 transition-all ${
              activeTab === 'ai'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-slate-500 hover:text-slate-800'
            }`}
          >
            <Sparkles className="w-3.5 h-3.5" />
            <span>AI Foundry Analysis</span>
          </button>
        </div>

        {/* Body Content */}
        <div className="p-6 overflow-y-auto space-y-6 text-xs">
          {activeTab === 'overview' && (
            <div className="space-y-6">
              {/* Cost Summary Box */}
              <div className="bg-gradient-to-r from-slate-50 to-blue-50/50 rounded-xl p-5 border border-slate-200">
                <h3 className="font-bold text-slate-800 mb-3 text-sm">Financial Impact Breakdown</h3>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-center">
                  <div className="bg-white p-3 rounded-lg border border-slate-200">
                    <span className="text-slate-500 text-[11px]">Current Monthly Spend</span>
                    <p className="text-base font-bold text-slate-800 mt-1">${costs.currentMonthlyCost.toFixed(2)}</p>
                    <span className="text-[10px] text-slate-400">{recommendation.currentSku || 'Current'}</span>
                  </div>
                  <div className="bg-white p-3 rounded-lg border border-slate-200">
                    <span className="text-slate-500 text-[11px]">Target Monthly Spend</span>
                    <p className="text-base font-bold text-slate-800 mt-1">${costs.targetMonthlyCost.toFixed(2)}</p>
                    <span className="text-[10px] text-slate-400">{recommendation.targetSku || 'Optimized'}</span>
                  </div>
                  <div className="bg-white p-3 rounded-lg border border-emerald-200 bg-emerald-50/30">
                    <span className="text-emerald-700 text-[11px] font-semibold">Net Realizable Savings</span>
                    <p className="text-lg font-extrabold text-emerald-600 mt-0.5">${costs.netMonthlySavings.toFixed(2)}</p>
                    <span className="text-[10px] text-emerald-600">/ month</span>
                  </div>
                  <div className="bg-white p-3 rounded-lg border border-purple-200 bg-purple-50/30">
                    <span className="text-purple-700 text-[11px]">Retained Storage Drag</span>
                    <p className="text-base font-bold text-purple-700 mt-1">${costs.retainedStorageCost.toFixed(2)}</p>
                    <span className="text-[10px] text-purple-600">Disks retained</span>
                  </div>
                </div>

                {costs.exclusionReason && (
                  <div className="mt-3 p-2.5 rounded-lg bg-amber-50 border border-amber-200 text-amber-800 text-xs flex items-start space-x-2">
                    <AlertTriangle className="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
                    <span><strong>Notice:</strong> {costs.exclusionReason}</span>
                  </div>
                )}
              </div>

              {/* Implementation Steps & Prerequisites */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="bg-white p-4 rounded-xl border border-slate-200">
                  <h4 className="font-bold text-slate-800 flex items-center mb-3">
                    <CheckSquare className="w-4 h-4 text-blue-600 mr-1.5" />
                    Implementation Prerequisites
                  </h4>
                  <ul className="space-y-2 text-slate-600">
                    {recommendation.prerequisites.map((step, idx) => (
                      <li key={idx} className="flex items-start space-x-2">
                        <span className="w-4 h-4 rounded-full bg-blue-100 text-blue-700 font-bold flex items-center justify-center text-[10px] shrink-0 mt-0.5">
                          {idx + 1}
                        </span>
                        <span>{step}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                {/* Rollback Strategy */}
                <div className="bg-white p-4 rounded-xl border border-slate-200">
                  <h4 className="font-bold text-slate-800 flex items-center mb-3">
                    <RotateCcw className="w-4 h-4 text-emerald-600 mr-1.5" />
                    Verified Rollback Guidance
                  </h4>
                  <ul className="space-y-2 text-slate-600">
                    {recommendation.rollbackGuidance.map((step, idx) => (
                      <li key={idx} className="flex items-start space-x-2 font-mono text-[11px] bg-slate-50 p-2 rounded border border-slate-200">
                        <span>{step}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>
          )}

          {activeTab === 'evidence' && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                {evidence.p95CpuPercent != null && (
                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 text-center">
                    <span className="text-slate-500 font-medium">Historical P95 CPU</span>
                    <div className="text-2xl font-black text-slate-800 mt-1">{evidence.p95CpuPercent.toFixed(1)}%</div>
                    <span className="text-[10px] text-slate-400">Target safe limit: 70%</span>
                  </div>
                )}
                {evidence.averageCpuPercent != null && (
                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 text-center">
                    <span className="text-slate-500 font-medium">Average CPU</span>
                    <div className="text-2xl font-black text-slate-800 mt-1">{evidence.averageCpuPercent.toFixed(1)}%</div>
                    <span className="text-[10px] text-slate-400">Baseline load</span>
                  </div>
                )}
                {evidence.daysUnattached != null && (
                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 text-center">
                    <span className="text-slate-500 font-medium">Days Unattached</span>
                    <div className="text-2xl font-black text-rose-600 mt-1">{evidence.daysUnattached} days</div>
                    <span className="text-[10px] text-slate-400">Idle storage</span>
                  </div>
                )}
                {evidence.connectedAppsCount != null && (
                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 text-center">
                    <span className="text-slate-500 font-medium">Active App Count</span>
                    <div className="text-2xl font-black text-slate-800 mt-1">{evidence.connectedAppsCount} apps</div>
                    <span className="text-[10px] text-slate-400">0 apps = empty plan</span>
                  </div>
                )}
              </div>

              {/* Observations */}
              <div className="bg-white p-4 rounded-xl border border-slate-200">
                <h4 className="font-bold text-slate-800 mb-2">Telemetry Observations</h4>
                <ul className="list-disc list-inside space-y-1 text-slate-600">
                  {evidence.observations.map((obs, idx) => (
                    <li key={idx}>{obs}</li>
                  ))}
                </ul>
              </div>

              {/* Tags */}
              <div className="bg-white p-4 rounded-xl border border-slate-200">
                <h4 className="font-bold text-slate-800 mb-2">Resource Tags</h4>
                <div className="flex flex-wrap gap-2">
                  {Object.entries(evidence.tags).length === 0 ? (
                    <span className="text-slate-400">No tags configured</span>
                  ) : (
                    Object.entries(evidence.tags).map(([k, v]) => (
                      <span key={k} className="bg-slate-100 text-slate-700 font-mono text-[11px] px-2.5 py-1 rounded-md border border-slate-200">
                        {k}: <strong>{v}</strong>
                      </span>
                    ))
                  )}
                </div>
              </div>
            </div>
          )}

          {activeTab === 'ai' && (
            <div className="space-y-4">
              {aiExplanation ? (
                <div className="space-y-4">
                  {/* Executive Summary */}
                  <div className="bg-gradient-to-r from-indigo-50/80 to-blue-50/80 p-4 rounded-xl border border-indigo-200">
                    <div className="flex items-center space-x-2 text-indigo-900 font-bold mb-1">
                      <Sparkles className="w-4 h-4 text-indigo-600" />
                      <h4>Executive Summary</h4>
                    </div>
                    <p className="text-slate-700 leading-relaxed">{aiExplanation.executiveSummary}</p>
                    <div className="mt-2 text-[10px] text-indigo-600 font-mono">
                      Generated by {aiExplanation.modelUsed || 'Azure AI Foundry'}
                    </div>
                  </div>

                  {/* Workload Context */}
                  <div className="bg-white p-4 rounded-xl border border-slate-200">
                    <h4 className="font-bold text-slate-800 mb-1.5">Workload Context & Capacity Analysis</h4>
                    <p className="text-slate-600 leading-relaxed">{aiExplanation.workloadContextAnalysis}</p>
                  </div>

                  {/* Risk Assessment */}
                  <div className="bg-white p-4 rounded-xl border border-slate-200">
                    <h4 className="font-bold text-slate-800 mb-1.5">Architectural Risk Assessment</h4>
                    <p className="text-slate-600 leading-relaxed">{aiExplanation.riskAssessment}</p>
                  </div>

                  {/* Tradeoffs */}
                  <div className="bg-white p-4 rounded-xl border border-slate-200">
                    <h4 className="font-bold text-slate-800 mb-1.5">Tradeoffs & Alternatives</h4>
                    <p className="text-slate-600 leading-relaxed">{aiExplanation.tradeoffsAndAlternatives}</p>
                  </div>
                </div>
              ) : (
                <div className="text-center py-10 bg-slate-50 rounded-xl border border-slate-200 space-y-3">
                  <Sparkles className="w-8 h-8 mx-auto text-indigo-400" />
                  <p className="font-medium text-slate-700">AI context explanation not generated yet</p>
                  <p className="text-xs text-slate-500 max-w-sm mx-auto">
                    Generate an in-depth architectural justification using Azure AI Foundry project <strong className="font-mono">finops-advisor-project</strong>.
                  </p>
                  <button
                    type="button"
                    onClick={() => onExplainOnDemand(recommendation.id)}
                    disabled={isExplaining}
                    className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-lg shadow-sm transition-all text-xs"
                  >
                    {isExplaining ? 'Generating Analysis...' : 'Generate AI Foundry Analysis'}
                  </button>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-slate-200 bg-slate-50 flex items-center justify-between">
          <span className="text-slate-400 text-[11px] font-mono">ID: {recommendation.id}</span>
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-1.5 bg-slate-200 hover:bg-slate-300 text-slate-800 rounded-lg text-xs font-medium transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
