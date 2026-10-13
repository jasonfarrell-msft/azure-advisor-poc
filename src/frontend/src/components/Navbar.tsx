import React from 'react';
import { AdvisorMode, SubscriptionInfo } from '../types';
import { Play, Sparkles, Cloud, Database, ShieldCheck, RefreshCw } from 'lucide-react';

interface NavbarProps {
  mode: AdvisorMode;
  onModeChange: (newMode: AdvisorMode) => void;
  subscriptions: SubscriptionInfo[];
  selectedSubscriptionId: string;
  onSubscriptionChange: (id: string) => void;
  onRunScan: () => void;
  isScanning: boolean;
  enrichWithAi: boolean;
  onEnrichWithAiChange: (val: boolean) => void;
  foundryConfigured: boolean;
  foundryDeployment?: string;
}

export const Navbar: React.FC<NavbarProps> = ({
  mode,
  onModeChange,
  subscriptions,
  selectedSubscriptionId,
  onSubscriptionChange,
  onRunScan,
  isScanning,
  enrichWithAi,
  onEnrichWithAiChange,
  foundryConfigured,
  foundryDeployment
}) => {
  return (
    <header className="bg-slate-900 border-b border-slate-800 text-white sticky top-0 z-30 shadow-md">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo & Title */}
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-blue-600 to-indigo-500 flex items-center justify-center shadow-lg shadow-blue-500/30">
              <Cloud className="w-6 h-6 text-white" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="font-bold text-lg tracking-tight text-white">Azure Cost Advisor</span>
                <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-blue-500/20 text-blue-300 border border-blue-500/30">
                  FinOps Agent
                </span>
              </div>
              <p className="text-xs text-slate-400">Workload-aware optimization & deterministic analytics</p>
            </div>
          </div>

          {/* AI Foundry Status Badge */}
          <div className="hidden lg:flex items-center space-x-2 text-xs bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700/60">
            {foundryConfigured ? (
              <>
                <Sparkles className="w-3.5 h-3.5 text-indigo-400 animate-pulse" />
                <span className="text-slate-300">
                  Foundry configured{foundryDeployment ? ` · ${foundryDeployment}` : ''}
                </span>
              </>
            ) : (
              <span className="text-slate-300">Deterministic AI fallback</span>
            )}
          </div>

          {/* Mode Switcher & Controls */}
          <div className="flex items-center space-x-3">
            {/* Live vs Demo Pill */}
            <div className="bg-slate-800 p-1 rounded-lg border border-slate-700 flex items-center">
              <button
                type="button"
                onClick={() => onModeChange('Demo')}
                className={`px-3 py-1 rounded-md text-xs font-medium transition-all ${
                  mode === 'Demo'
                    ? 'bg-indigo-600 text-white shadow-sm'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                <Database className="w-3.5 h-3.5 inline mr-1.5" />
                Demo Mode
              </button>
              <button
                type="button"
                onClick={() => onModeChange('Live')}
                className={`px-3 py-1 rounded-md text-xs font-medium transition-all ${
                  mode === 'Live'
                    ? 'bg-emerald-600 text-white shadow-sm'
                    : 'text-slate-400 hover:text-white'
                }`}
              >
                <ShieldCheck className="w-3.5 h-3.5 inline mr-1.5" />
                Live Azure
              </button>
            </div>

            {/* Subscription Selector */}
            <select
              aria-label="Azure subscription"
              value={selectedSubscriptionId}
              onChange={(e) => onSubscriptionChange(e.target.value)}
              className="bg-slate-800 text-slate-200 text-xs rounded-lg border border-slate-700 px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500 max-w-sm"
            >
              {subscriptions.map((sub) => (
                <option key={sub.subscriptionId} value={sub.subscriptionId}>
                  {sub.displayName}{sub.optimizationFocus ? ` — ${sub.optimizationFocus}` : ''}
                </option>
              ))}
            </select>

            {/* AI Toggle */}
            <label className="hidden sm:flex items-center space-x-1.5 text-xs text-slate-300 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={enrichWithAi}
                onChange={(e) => onEnrichWithAiChange(e.target.checked)}
                className="rounded border-slate-700 text-blue-600 focus:ring-blue-500 bg-slate-800"
              />
              <span>AI Explanations</span>
            </label>

            {/* Scan Button */}
            <button
              type="button"
              onClick={onRunScan}
              disabled={isScanning || !selectedSubscriptionId}
              className={`flex items-center space-x-2 px-4 py-1.5 rounded-lg text-xs font-semibold shadow-md transition-all ${
                isScanning
                  ? 'bg-slate-700 text-slate-400 cursor-not-allowed'
                  : 'bg-blue-600 hover:bg-blue-500 text-white shadow-blue-600/30'
              }`}
            >
              {isScanning ? (
                <>
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  <span>Scanning...</span>
                </>
              ) : (
                <>
                  <Play className="w-3.5 h-3.5 fill-current" />
                  <span>Scan Azure</span>
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </header>
  );
};
