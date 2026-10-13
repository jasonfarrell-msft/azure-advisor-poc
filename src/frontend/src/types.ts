export type AdvisorMode = 'Live' | 'Demo';

export type RecommendationCategory = 'Rightsizing' | 'Scheduling' | 'Removal';

export type RecommendationAction = 'Resize' | 'ScheduledShutdown' | 'DeleteOrphan';

export type RiskLevel = 'Safe' | 'Low' | 'Medium' | 'High';

export type ConfidenceLevel = 'Low' | 'Medium' | 'High';

export type RecommendationStatus = 'Candidate' | 'Actionable' | 'ReviewRequired' | 'Overlapped' | 'Suppressed';

export interface SubscriptionInfo {
  subscriptionId: string;
  displayName: string;
  tenantId?: string;
  state: string;
  isDemo: boolean;
  optimizationFocus?: string;
  scenarioSummary?: string;
}

export interface CostBreakdown {
  currentMonthlyCost: number;
  targetMonthlyCost: number;
  grossMonthlySavings: number;
  netMonthlySavings: number;
  retainedStorageCost: number;
  retainedNetworkingCost: number;
  currency: string;
  basis: string;
  exclusionReason?: string;
}

export interface SchedulePlan {
  offPeakHoursPerWeek: number;
  totalHoursPerMonth: number;
  deallocatedHoursPerMonth: number;
  activeHoursPerMonth: number;
  timeZone: string;
  eligibleDays: number[];
  dailyStartHour: number;
  dailyStopHour: number;
}

export interface EvidenceSummary {
  telemetryWindowDays: number;
  averageCpuPercent?: number;
  p95CpuPercent?: number;
  averageMemoryPercent?: number;
  p95MemoryPercent?: number;
  daysUnattached?: number;
  connectedAppsCount?: number;
  tags: Record<string, string>;
  observations: string[];
}

export interface AiExplanationResult {
  executiveSummary: string;
  workloadContextAnalysis: string;
  riskAssessment: string;
  implementationSteps: string[];
  rollbackStrategy: string[];
  tradeoffsAndAlternatives: string;
  isAiGenerated: boolean;
  modelUsed?: string;
  generatedAtUtc: string;
}

export interface RecommendationCard {
  id: string;
  resourceId: string;
  resourceName: string;
  resourceType: string;
  resourceGroup: string;
  subscriptionId: string;
  location: string;
  category: RecommendationCategory;
  action: RecommendationAction;
  title: string;
  description: string;
  currentSku?: string;
  targetSku?: string;
  costs: CostBreakdown;
  schedule?: SchedulePlan;
  risk: RiskLevel;
  confidence: ConfidenceLevel;
  status: RecommendationStatus;
  prerequisites: string[];
  rollbackGuidance: string[];
  whyNotAlternatives: string[];
  evidence: EvidenceSummary;
  aiExplanation?: AiExplanationResult;
}

export interface AnalysisResult {
  jobId: string;
  subscriptionId: string;
  mode: number;
  startedAtUtc: string;
  completedAtUtc: string;
  totalResourcesScanned: number;
  totalGrossSavingsMonthly: number;
  totalNetRealizableSavingsMonthly: number;
  recommendations: RecommendationCard[];
  categorySavings: Record<string, number>;
  riskCounts: Record<string, number>;
  categoryCounts: Record<string, number>;
}

export interface ScheduleCalculationResult {
  recommendationId: string;
  weeklyActiveHours: number;
  weeklyIdleHours: number;
  newNetMonthlySavings: number;
  retainedStorageMonthlyCost: number;
  avoidedComputeMonthlySavings: number;
}
