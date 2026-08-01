export type ViewId = 'overview' | 'inventory' | 'review' | 'findings' | 'compare' | 'operations' | 'decisions' | 'command' | 'exports' | 'system';
export type InventoryViewMode = 'table' | 'tree';
export type ArtifactSort = 'path' | 'size-desc' | 'findings-desc' | 'status';

export interface ProjectSummary {
  id: string;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  importCount: number;
  latestImportId: string | null;
  latestImportStatus: string | null;
}

export interface ImportSummary {
  id: string;
  projectId: string;
  displayName: string;
  status: string;
  currentStage: string;
  statusMessage: string | null;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  totalFiles: number;
  processedFiles: number;
  warningCount: number;
  errorCount: number;
  totalBytes: number;
  cancellationRequested: boolean;
}

export interface ArtifactListItem {
  id: string;
  importSnapshotId: string;
  parentArtifactId: string | null;
  name: string;
  relativePath: string;
  extension: string;
  mediaType: string;
  sizeBytes: number;
  sha256: string;
  parseStatus: string;
  parserId: string | null;
  parserVersion: string | null;
  importedAtUtc: string;
  findingCount: number;
  reviewStatus: ReviewStatus;
  reviewUpdatedAtUtc: string | null;
}

export interface ArtifactPage {
  total: number;
  offset: number;
  limit: number;
  items: ArtifactListItem[];
}

export interface Finding {
  id: string;
  importSnapshotId: string;
  artifactId: string | null;
  artifactPath: string | null;
  severity: 'Info' | 'Warning' | 'Error' | string;
  ruleId: string;
  title: string;
  message: string;
  sourceLocation: string | null;
  evidenceExcerpt: string | null;
  recommendation: string | null;
  createdAtUtc: string;
}

export type ReviewStatus = 'Unreviewed' | 'InReview' | 'Accepted' | 'NeedsAttention';

export interface ArtifactReview {
  status: ReviewStatus;
  note: string | null;
  tags: string[];
  updatedAtUtc: string | null;
}

export interface ArtifactDetail {
  artifact: ArtifactListItem;
  structureSummary: unknown | null;
  previewText: string | null;
  parseError: string | null;
  findings: Finding[];
  review: ArtifactReview;
}

export interface ArtifactDifference {
  relativePath: string;
  changeType: 'Added' | 'Removed' | 'Modified' | 'Unchanged';
  leftSha256: string | null;
  rightSha256: string | null;
  leftSizeBytes: number | null;
  rightSizeBytes: number | null;
}

export interface CompareResult {
  leftImportId: string;
  rightImportId: string;
  addedCount: number;
  removedCount: number;
  modifiedCount: number;
  unchangedCount: number;
  differences: ArtifactDifference[];
}

export interface SearchResult {
  artifacts: ArtifactListItem[];
  findings: Finding[];
}

export interface ApiErrorShape {
  title?: string;
  detail?: string;
  error?: string;
  errors?: Record<string, string[]>;
}

export interface ArtifactTreeNode {
  id: string;
  name: string;
  path: string;
  kind: 'folder' | 'artifact';
  children: ArtifactTreeNode[];
  artifact?: ArtifactListItem;
  aggregateSize: number;
  findingCount: number;
  reviewStatus: ReviewStatus;
  reviewUpdatedAtUtc: string | null;
}

export interface WorkspaceInsights {
  artifactCount: number;
  parsedCount: number;
  unsupportedCount: number;
  failedCount: number;
  duplicateArtifactCount: number;
  duplicateGroupCount: number;
  totalBytes: number;
  parseCoveragePercent: number;
  healthScore: number;
  healthLabel: string;
  typeDistribution: Array<{ label: string; count: number; bytes: number; percent: number }>;
  statusDistribution: Array<{ label: string; count: number; percent: number }>;
}


export interface AgentStatus {
  status: string;
  service: string;
  version: string;
  timestampUtc: string;
  uptimeSeconds: number;
  workspaceFreeBytes: number;
  workspaceTotalBytes: number;
  databaseSizeBytes: number;
  projectCount: number;
  importCount: number;
  artifactCount: number;
  findingCount: number;
  queuedImportCount: number;
  parsers: string[];
  limits: {
    maximumUploadBytes: number;
    maximumSingleFileBytes: number;
    maximumExtractedBytes: number;
    maximumExtractedFiles: number;
    maximumCompressionRatio: number;
  };
}

export interface WatchFolder {
  id: string;
  projectId: string;
  name: string;
  folderPath: string;
  enabled: boolean;
  triggerMode: 'Manual' | 'Hourly' | 'Daily';
  scanIntervalMinutes: number;
  ignorePatterns: string[];
  requireApproval: boolean;
  lastScannedAtUtc: string | null;
  lastImportId: string | null;
}

export interface DataProfile {
  id: string;
  artifactId: string;
  artifactPath: string;
  profileType: string;
  metrics: Record<string, unknown>;
  issues: Array<{ code?: string; message?: string }>;
  createdAtUtc: string;
}

export interface LineageEdge {
  id: string;
  fromArtifactId: string;
  fromPath: string;
  toArtifactId: string | null;
  toPath: string | null;
  edgeType: string;
  label: string;
  evidence: Record<string, unknown> | null;
}

export interface PrivacyDetection {
  id: string;
  artifactId: string;
  artifactPath: string;
  kind: string;
  severity: string;
  sourceLocation: string;
  maskedPreview: string;
  status: string;
}

export interface PlaybookStep {
  id: string;
  name: string;
  type: string;
  required: boolean;
  configuration?: Record<string, unknown> | null;
}

export interface Playbook {
  id: string;
  projectId: string;
  name: string;
  description: string;
  steps: PlaybookStep[];
  status: string;
  progressPercent: number;
  lastRunSummary: string | null;
  lastRunAtUtc: string | null;
}


export interface TriageFactor { name: string; points: number; explanation: string; }
export interface TriageItem {
  artifactId: string; artifactPath: string; priorityScore: number; priorityBand: string; reviewStatus: ReviewStatus;
  findingCount: number; impactCount: number; privacyCount: number; factors: TriageFactor[];
}

export interface BaselineRule { metric: string; operator: '<=' | '>=' | '=='; value: number; severity: 'Error' | 'Warning' | string; }
export interface BaselineRuleResult { metric: string; operator: '<=' | '>=' | '=='; expected: number; actual: number; passed: boolean; severity: 'Error' | 'Warning' | string; message: string; }
export interface BaselinePolicy {
  id: string; projectId: string; name: string; baselineImportId: string; rules: BaselineRule[]; status: string;
  lastEvaluatedImportId: string | null; lastResult: unknown | null; lastEvaluatedAtUtc: string | null;
}
export interface BaselineEvaluation {
  policyId: string; baselineImportId: string; currentImportId: string; status: string; passedRules: number; failedRules: number;
  results: BaselineRuleResult[]; evaluatedAtUtc: string;
}

export interface AutomationStep { id: string; name: string; type: string; required: boolean; configuration?: Record<string, unknown> | null; }
export interface AutomationRecipe {
  id: string; projectId: string; name: string; description: string; steps: AutomationStep[]; enabled: boolean; triggerMode: string;
  scheduleIntervalMinutes: number; status: string; progressPercent: number; lastRunSummary: string | null; lastRunAtUtc: string | null;
}

export interface EvidenceCitation { artifactId: string | null; findingId: string | null; artifactPath: string; sourceLocation: string | null; excerpt: string; basis: string; }
export interface EvidenceAnswer { answer: string; confidence: string; citations: EvidenceCitation[]; followUpQueries: string[]; }


export interface QueueWeight { metric: string; multiplier: number; explanation: string; }
export interface QueuePolicy { id: string; projectId: string; name: string; weights: QueueWeight[]; slaHours: number; active: boolean; createdAtUtc: string; updatedAtUtc: string; }
export interface QueueReason { name: string; points: number; explanation: string; }
export interface AdaptiveQueueItem { artifactId: string; artifactPath: string; score: number; band: string; reviewStatus: ReviewStatus; dueAtUtc: string; slaState: string; reasons: QueueReason[]; rank: number; }
export interface ScenarioAssumption { metric: string; delta: number; description: string; }
export interface ScenarioMetric { metric: string; current: number; projected: number; delta: number; }
export interface ScenarioResult { currentReadinessScore: number; projectedReadinessScore: number; scoreDelta: number; projectedStatus: string; metrics: ScenarioMetric[]; recommendations: string[]; }
export interface ScenarioRun { id: string; projectId: string; importSnapshotId: string; name: string; assumptions: ScenarioAssumption[]; result: ScenarioResult; createdAtUtc: string; }
export interface ApprovalRequirement { name: string; passed: boolean; evidence: string; }
export interface ApprovalGate { id: string; projectId: string; importSnapshotId: string; name: string; gateType: string; requiredRole: string; status: string; requirements: ApprovalRequirement[]; decidedBy: string | null; rationale: string | null; createdAtUtc: string; decidedAtUtc: string | null; }
export interface AnomalyEvidence { findingId: string; artifactId: string | null; sourceLocation: string | null; excerpt: string | null; ruleId: string; }
export interface AnomalyExplanation { artifactId: string | null; artifactPath: string; severity: string; title: string; observed: string; expected: string; drivers: string[]; evidence: AnomalyEvidence[]; impact: string; recommendedAction: string; }
export interface ExecutivePriority { rank: number; artifactPath: string; score: number; band: string; drivers: string[]; }
export interface ExecutiveSummary { importId: string; readinessScore: number; status: string; queueItems: number; criticalPriorities: number; pendingApprovals: number; regressedPolicies: number; metrics: Record<string, number>; highlights: string[]; topPriorities: ExecutivePriority[]; generatedAtUtc: string; }
