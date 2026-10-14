import React from 'react';
import { AnalysisResult } from '../types';
import { DollarSign, TrendingDown, Layers, CheckCircle2, AlertTriangle } from 'lucide-react';

interface KpiSummaryProps {
  analysis: AnalysisResult | null;
}

export const KpiSummary: React.FC<KpiSummaryProps> = ({ analysis }) => {
  if (!analysis) {
    return (
      <div className="bg-white rounded-xl border border-slate-200 p-8 text-center text-slate-500 shadow-sm">
        <Layers className="w-10 h-10 mx-auto text-slate-400 mb-3 animate-bounce" />
        <p className="font-medium text-slate-700">No active scan results</p>
        <p className="text-sm">Select a subscription above and click &quot;Scan Azure&quot; to inspect workloads and calculate cost reductions.</p>
      </div>
    );
  }

  const netMonthly = analysis.totalNetRealizableSavingsMonthly;
  const grossMonthly = analysis.totalGrossSavingsMonthly;
  const annualNet = netMonthly * 12;
  const overlappedCount = analysis.recommendations.filter(r => r.status === 'Overlapped').length;
  const actionableCount = analysis.recommendations.filter(r => r.status === 'Actionable').length;

  return (
    <div className="space-y-4">
      {/* KPI Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {/* Net Realizable Savings */}
        <div className="bg-gradient-to-br from-emerald-900 to-slate-900 text-white rounded-xl p-5 shadow-sm border border-emerald-800/40 relative overflow-hidden">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium uppercase tracking-wider text-emerald-300">Net Realizable Savings</span>
            <span className="p-1.5 rounded-lg bg-emerald-500/20 text-emerald-400">
              <DollarSign className="w-4 h-4" />
            </span>
          </div>
          <div className="mt-3 flex items-baseline space-x-2">
            <span className="text-3xl font-extrabold tracking-tight text-white">
              ${netMonthly.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
            <span className="text-xs text-emerald-300 font-medium">/ month</span>
          </div>
          <div className="mt-2 text-xs text-emerald-200/80 flex items-center space-x-1">
            <TrendingDown className="w-3.5 h-3.5 text-emerald-400 inline" />
            <span>${annualNet.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })} annual run-rate reduction</span>
          </div>
        </div>

        {/* Gross Avoidable Spend */}
        <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">Gross Avoidable Spend</span>
            <span className="p-1.5 rounded-lg bg-blue-50 text-blue-600">
              <TrendingDown className="w-4 h-4" />
            </span>
          </div>
          <div className="mt-3 flex items-baseline space-x-2">
            <span className="text-3xl font-extrabold tracking-tight text-slate-800">
              ${grossMonthly.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
            <span className="text-xs text-slate-500 font-medium">/ month</span>
          </div>
          <div className="mt-2 text-xs text-slate-500">
            Before portfolio conflict resolution
          </div>
        </div>

        {/* Scanned Inventory */}
        <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">Scanned Resources</span>
            <span className="p-1.5 rounded-lg bg-indigo-50 text-indigo-600">
              <Layers className="w-4 h-4" />
            </span>
          </div>
          <div className="mt-3 flex items-baseline space-x-2">
            <span className="text-3xl font-extrabold tracking-tight text-slate-800">
              {analysis.totalResourcesScanned}
            </span>
            <span className="text-xs text-slate-500 font-medium">assets evaluated</span>
          </div>
          <div className="mt-2 text-xs text-slate-500 flex items-center space-x-1">
            <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500 inline" />
            <span>{actionableCount} actionable recommendations</span>
          </div>
        </div>

        {/* Double-Count Protection */}
        <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">Conflict Resolver</span>
            <span className="p-1.5 rounded-lg bg-amber-50 text-amber-600">
              <AlertTriangle className="w-4 h-4" />
            </span>
          </div>
          <div className="mt-3 flex items-baseline space-x-2">
            <span className="text-3xl font-extrabold tracking-tight text-slate-800">
              {overlappedCount}
            </span>
            <span className="text-xs text-amber-600 font-medium font-semibold">overlaps resolved</span>
          </div>
          <div className="mt-2 text-xs text-slate-500">
            Double-counting mathematically eliminated
          </div>
        </div>
      </div>

      {/* Category Breakdown Bar */}
      <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-sm flex flex-wrap items-center justify-between gap-3 text-xs">
        <span className="font-semibold text-slate-700 flex items-center">
          Savings by Vector:
        </span>
        <div className="flex flex-wrap items-center gap-3">
          {Object.entries(analysis.categorySavings).map(([cat, amount]) => (
            <div key={cat} className="flex items-center space-x-2 bg-slate-50 border border-slate-200 px-3 py-1.5 rounded-lg">
              <span className={`w-2 h-2 rounded-full ${
                cat === 'Reservation' ? 'bg-emerald-500' :
                cat === 'Rightsizing' ? 'bg-blue-500' :
                cat === 'Scheduling' ? 'bg-purple-500' : 'bg-rose-500'
              }`} />
              <span className="font-medium text-slate-700">{cat}:</span>
              <span className="font-bold text-slate-900">${amount.toFixed(2)}/mo</span>
            </div>
          ))}
        </div>
        <div className="text-slate-400 text-xs hidden lg:block">
          Scan ID: <span className="font-mono text-slate-600">{analysis.jobId.substring(0, 12)}...</span>
        </div>
      </div>
    </div>
  );
};
