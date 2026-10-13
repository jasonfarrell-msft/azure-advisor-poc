import { AnalysisResult, RecommendationCard, ScheduleCalculationResult, SubscriptionInfo } from './types';

const API_BASE = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/$/, '');

export async function fetchMode(): Promise<string> {
  const res = await fetch(`${API_BASE}/settings/mode`);
  if (!res.ok) throw new Error('Failed to fetch advisor mode');
  const data = await res.json();
  return data.mode;
}

export async function fetchSubscriptions(mode?: string): Promise<SubscriptionInfo[]> {
  const url = mode ? `${API_BASE}/subscriptions?mode=${mode}` : `${API_BASE}/subscriptions`;
  const res = await fetch(url);
  if (!res.ok) {
    const errorData = await res.json().catch(() => ({}));
    throw new Error(errorData.detail || errorData.title || 'Failed to fetch subscriptions');
  }
  const data = await res.json();
  return data.subscriptions || [];
}

export async function runAnalysisScan(
  subscriptionId: string,
  mode: string,
  enrichWithAi: boolean = true
): Promise<AnalysisResult> {
  const res = await fetch(`${API_BASE}/analysis/scan`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ subscriptionId, mode, enrichWithAi })
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.detail || err.title || 'Failed to complete analysis scan');
  }
  return res.json();
}

export async function fetchLatestAnalysis(): Promise<AnalysisResult | null> {
  const res = await fetch(`${API_BASE}/analysis/latest`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error('Failed to load latest analysis');
  return res.json();
}

export async function explainRecommendation(id: string): Promise<RecommendationCard['aiExplanation']> {
  const res = await fetch(`${API_BASE}/recommendations/${encodeURIComponent(id)}/explain`, {
    method: 'POST'
  });
  if (!res.ok) throw new Error('Failed to generate AI explanation');
  return res.json();
}

export async function recalculateSchedule(
  recommendationId: string,
  startHour: number,
  stopHour: number,
  includeWeekends: boolean
): Promise<ScheduleCalculationResult> {
  const res = await fetch(`${API_BASE}/schedules/calculate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ recommendationId, startHour, stopHour, includeWeekends })
  });
  if (!res.ok) throw new Error('Failed to recalculate schedule savings');
  return res.json();
}
