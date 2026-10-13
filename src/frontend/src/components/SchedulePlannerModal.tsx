import React, { useState, useEffect } from 'react';
import { RecommendationCard, ScheduleCalculationResult } from '../types';
import { recalculateSchedule } from '../api';
import { X, Calendar, Clock, Loader2 } from 'lucide-react';

interface SchedulePlannerModalProps {
  recommendation: RecommendationCard | null;
  onClose: () => void;
}

export const SchedulePlannerModal: React.FC<SchedulePlannerModalProps> = ({
  recommendation,
  onClose
}) => {
  const [startHour, setStartHour] = useState<number>(7);
  const [stopHour, setStopHour] = useState<number>(19);
  const [includeWeekends, setIncludeWeekends] = useState<boolean>(false);
  const [calculation, setCalculation] = useState<ScheduleCalculationResult | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);

  useEffect(() => {
    if (!recommendation) return;

    let isMounted = true;
    setIsLoading(true);

    recalculateSchedule(recommendation.id, startHour, stopHour, includeWeekends)
      .then((res) => {
        if (isMounted) setCalculation(res);
      })
      .catch(() => {
        // Handle error gracefully
      })
      .finally(() => {
        if (isMounted) setIsLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [recommendation, startHour, stopHour, includeWeekends]);

  if (!recommendation) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4 overflow-y-auto">
      <div className="bg-white rounded-2xl shadow-2xl border border-slate-200 w-full max-w-xl overflow-hidden animate-in fade-in zoom-in-95 duration-150">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-200 bg-purple-50/60 flex items-center justify-between">
          <div className="flex items-center space-x-2.5">
            <div className="w-8 h-8 rounded-lg bg-purple-600 text-white flex items-center justify-center">
              <Calendar className="w-4 h-4" />
            </div>
            <div>
              <h3 className="font-bold text-slate-900 text-sm">Interactive Schedule Simulator</h3>
              <p className="text-xs text-slate-500 font-mono">{recommendation.resourceName}</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1 rounded-lg text-slate-400 hover:text-slate-600 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-6 text-xs">
          {/* Hour Controls */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-slate-700 font-semibold mb-1.5 flex items-center">
                <Clock className="w-3.5 h-3.5 mr-1 text-slate-400" />
                Startup Hour (UTC)
              </label>
              <select
                value={startHour}
                onChange={(e) => setStartHour(parseInt(e.target.value, 10))}
                className="w-full bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-slate-800 font-medium focus:ring-2 focus:ring-purple-500"
              >
                {Array.from({ length: 24 }).map((_, h) => (
                  <option key={h} value={h}>
                    {h.toString().padStart(2, '0')}:00 UTC ({h > 12 ? `${h - 12} PM` : h === 0 ? '12 AM' : `${h} AM`})
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-slate-700 font-semibold mb-1.5 flex items-center">
                <Clock className="w-3.5 h-3.5 mr-1 text-slate-400" />
                Shutdown Hour (UTC)
              </label>
              <select
                value={stopHour}
                onChange={(e) => setStopHour(parseInt(e.target.value, 10))}
                className="w-full bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-slate-800 font-medium focus:ring-2 focus:ring-purple-500"
              >
                {Array.from({ length: 24 }).map((_, h) => (
                  <option key={h} value={h}>
                    {h.toString().padStart(2, '0')}:00 UTC ({h > 12 ? `${h - 12} PM` : h === 0 ? '12 AM' : `${h} AM`})
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Weekend Toggle */}
          <label className="flex items-center space-x-2 cursor-pointer bg-slate-50 p-3 rounded-lg border border-slate-200">
            <input
              type="checkbox"
              checked={includeWeekends}
              onChange={(e) => setIncludeWeekends(e.target.checked)}
              className="rounded border-slate-300 text-purple-600 focus:ring-purple-500 w-4 h-4"
            />
            <span className="text-slate-700 font-medium">Keep running during weekends (Saturday & Sunday)</span>
          </label>

          {/* Dynamic Calculated Financials */}
          {isLoading && (
            <div className="flex items-center justify-center p-6 text-slate-500">
              <Loader2 className="w-5 h-5 animate-spin mr-2 text-purple-600" />
              <span>Simulating schedule savings...</span>
            </div>
          )}
          {!isLoading && calculation && (
            <div className="bg-gradient-to-br from-slate-900 to-purple-950 text-white rounded-xl p-5 space-y-4 shadow-md">
              <div className="flex items-center justify-between border-b border-purple-800/40 pb-3">
                <span className="text-purple-300 font-medium text-xs">Simulated Realizable Monthly Savings</span>
                <span className="text-2xl font-black text-emerald-400">
                  ${calculation.newNetMonthlySavings.toFixed(2)}
                  <span className="text-xs font-normal text-purple-200">/mo</span>
                </span>
              </div>

              <div className="grid grid-cols-3 gap-2 text-center text-xs">
                <div className="bg-purple-900/40 p-2.5 rounded-lg border border-purple-700/30">
                  <span className="text-purple-300 block text-[10px]">Weekly Idle Hours</span>
                  <strong className="text-white text-base">{calculation.weeklyIdleHours}h</strong>
                </div>
                <div className="bg-purple-900/40 p-2.5 rounded-lg border border-purple-700/30">
                  <span className="text-purple-300 block text-[10px]">Weekly Active Hours</span>
                  <strong className="text-white text-base">{calculation.weeklyActiveHours}h</strong>
                </div>
                <div className="bg-purple-900/40 p-2.5 rounded-lg border border-purple-700/30">
                  <span className="text-purple-300 block text-[10px]">Retained Storage Drag</span>
                  <strong className="text-purple-300 text-base">${calculation.retainedStorageMonthlyCost.toFixed(2)}</strong>
                </div>
              </div>

              <p className="text-[11px] text-purple-200/80 leading-relaxed">
                Compute billing is completely halted during deallocated hours. OS and attached data disks remain retained in storage.
              </p>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-slate-200 bg-slate-50 flex items-center justify-end space-x-2">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-1.5 bg-purple-600 hover:bg-purple-500 text-white rounded-lg text-xs font-semibold shadow-sm transition-colors"
          >
            Apply Schedule
          </button>
        </div>
      </div>
    </div>
  );
};
