import type {
  AgentStatus,
  ApiErrorShape,
  ArtifactDetail,
  ArtifactPage,
  ArtifactReview,
  CompareResult,
  Finding,
  ImportSummary,
  ProjectSummary,
  ReviewStatus,
  SearchResult,
  WatchFolder,
  DataProfile,
  LineageEdge,
  PrivacyDetection,
  Playbook,
  PlaybookStep,
  TriageItem,
  BaselinePolicy,
  BaselineEvaluation,
  BaselineRule,
  AutomationRecipe,
  AutomationStep,
  EvidenceAnswer,
  QueuePolicy,
  QueueWeight,
  AdaptiveQueueItem,
  ScenarioRun,
  ScenarioAssumption,
  ApprovalGate,
  AnomalyExplanation,
  ExecutiveSummary,
} from './types';

export class ApiError extends Error {
  readonly status: number;
  readonly details: ApiErrorShape | null;

  constructor(message: string, status: number, details: ApiErrorShape | null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.details = details;
  }
}

const environmentBase = (import.meta.env.VITE_WORKBENCH_API_URL as string | undefined)?.trim();
const storedBase = localStorage.getItem('ws.apiBaseUrl')?.trim();
const apiBaseUrl = (storedBase || environmentBase || '').replace(/\/$/, '');

function url(path: string): string {
  return `${apiBaseUrl}${path}`;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url(path), {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    let details: ApiErrorShape | null = null;
    try { details = (await response.json()) as ApiErrorShape; } catch { /* non-JSON response */ }
    const validationMessage = details?.errors ? Object.values(details.errors).flat().join(' ') : null;
    throw new ApiError(
      validationMessage || details?.error || details?.detail || details?.title || `Request failed (${response.status}).`,
      response.status,
      details,
    );
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  baseUrl: apiBaseUrl,
  getAgentStatus: () => request<AgentStatus>('/api/system/status'),
  getProjects: () => request<ProjectSummary[]>('/api/projects'),
  createProject: (name: string) => request<ProjectSummary>('/api/projects', { method: 'POST', body: JSON.stringify({ name }) }),
  renameProject: (projectId: string, name: string) => request<ProjectSummary>(`/api/projects/${projectId}`, { method: 'PATCH', body: JSON.stringify({ name }) }),
  getImports: (projectId: string) => request<ImportSummary[]>(`/api/projects/${projectId}/imports`),
  getImport: (importId: string) => request<ImportSummary>(`/api/imports/${importId}`),
  createImport: (projectId: string, files: File[], displayName: string) => {
    const form = new FormData();
    for (const file of files) form.append('files', file, file.name);
    if (displayName.trim()) form.append('displayName', displayName.trim());
    return request<ImportSummary>(`/api/projects/${projectId}/imports`, { method: 'POST', body: form });
  },
  cancelImport: (importId: string) => request<ImportSummary>(`/api/imports/${importId}/cancel`, { method: 'POST' }),
  retryImport: (importId: string) => request<ImportSummary>(`/api/imports/${importId}/retry`, { method: 'POST' }),
  getArtifacts: (importId: string, search = '') => {
    const parameters = new URLSearchParams({ limit: '2000' });
    if (search.trim()) parameters.set('search', search.trim());
    return request<ArtifactPage>(`/api/imports/${importId}/artifacts?${parameters}`);
  },
  getArtifact: (artifactId: string) => request<ArtifactDetail>(`/api/artifacts/${artifactId}`),
  updateArtifactReview: (artifactId: string, status: ReviewStatus, note: string, tags: string[]) =>
    request<ArtifactReview>(`/api/artifacts/${artifactId}/review`, {
      method: 'PATCH',
      body: JSON.stringify({ status, note, tags }),
    }),
  artifactContentUrl: (artifactId: string) => url(`/api/artifacts/${artifactId}/content`),
  getFindings: (projectId: string, importId?: string) => {
    const parameters = new URLSearchParams();
    if (importId) parameters.set('importId', importId);
    const suffix = parameters.size > 0 ? `?${parameters}` : '';
    return request<Finding[]>(`/api/projects/${projectId}/findings${suffix}`);
  },
  search: (projectId: string, query: string, importId?: string) => {
    const parameters = new URLSearchParams({ q: query });
    if (importId) parameters.set('importId', importId);
    return request<SearchResult>(`/api/projects/${projectId}/search?${parameters}`);
  },
  compare: (projectId: string, leftImportId: string, rightImportId: string) =>
    request<CompareResult>(`/api/projects/${projectId}/compare`, { method: 'POST', body: JSON.stringify({ leftImportId, rightImportId }) }),
  exportUrl: (projectId: string, importId: string, format: 'json' | 'csv' | 'html') =>
    url(`/api/projects/${projectId}/imports/${importId}/export/${format}`),
  projectManifestUrl: (projectId: string) => url(`/api/projects/${projectId}/manifest`),
  getWatchFolders: (projectId: string) => request<WatchFolder[]>(`/api/projects/${projectId}/watch-folders`),
  createWatchFolder: (projectId: string, input: { name: string; folderPath: string; triggerMode: string; scanIntervalMinutes: number; ignorePatterns: string[]; requireApproval: boolean }) =>
    request<WatchFolder>(`/api/projects/${projectId}/watch-folders`, { method: 'POST', body: JSON.stringify(input) }),
  updateWatchFolder: (projectId: string, watchFolderId: string, input: Partial<{ name: string; enabled: boolean; triggerMode: string; scanIntervalMinutes: number; ignorePatterns: string[]; requireApproval: boolean }>) =>
    request<WatchFolder>(`/api/projects/${projectId}/watch-folders/${watchFolderId}`, { method: 'PATCH', body: JSON.stringify(input) }),
  scanWatchFolder: (projectId: string, watchFolderId: string, force = true) =>
    request<{ changed: boolean; importId: string | null; message: string; fileCount: number; totalBytes: number }>(`/api/projects/${projectId}/watch-folders/${watchFolderId}/scan?force=${force}`, { method: 'POST' }),
  runProfiles: (projectId: string, importId: string) => request<{ profiledArtifacts: number }>(`/api/projects/${projectId}/imports/${importId}/profiles/run`, { method: 'POST' }),
  getProfiles: (projectId: string, importId: string) => request<DataProfile[]>(`/api/projects/${projectId}/imports/${importId}/profiles`),
  rebuildLineage: (projectId: string, importId: string) => request<{ edges: number }>(`/api/projects/${projectId}/imports/${importId}/lineage/rebuild`, { method: 'POST' }),
  getLineage: (projectId: string, importId: string) => request<LineageEdge[]>(`/api/projects/${projectId}/imports/${importId}/lineage`),
  runPrivacyScan: (projectId: string, importId: string) => request<{ detections: number }>(`/api/projects/${projectId}/imports/${importId}/privacy/scan`, { method: 'POST' }),
  getPrivacyDetections: (projectId: string, importId: string) => request<PrivacyDetection[]>(`/api/projects/${projectId}/imports/${importId}/privacy`),
  updatePrivacyDetection: (projectId: string, importId: string, detectionId: string, status: 'Open' | 'Confirmed' | 'Dismissed' | 'Redacted') => request<{ id: string; status: string }>(`/api/projects/${projectId}/imports/${importId}/privacy/${detectionId}`, { method: 'PATCH', body: JSON.stringify({ status }) }),
  redactedExportUrl: (projectId: string, importId: string) => url(`/api/projects/${projectId}/imports/${importId}/privacy/redacted-export`),
  getPlaybooks: (projectId: string) => request<Playbook[]>(`/api/projects/${projectId}/playbooks`),
  createPlaybook: (projectId: string, input: { name: string; description: string; steps: PlaybookStep[] }) => request<Playbook>(`/api/projects/${projectId}/playbooks`, { method: 'POST', body: JSON.stringify(input) }),
  runPlaybook: (projectId: string, playbookId: string, importId: string) => request<Playbook>(`/api/projects/${projectId}/playbooks/${playbookId}/run/${importId}`, { method: 'POST' }),
  getTriage: (projectId: string, importId: string) => request<TriageItem[]>(`/api/projects/${projectId}/imports/${importId}/triage`),
  getBaselines: (projectId: string) => request<BaselinePolicy[]>(`/api/projects/${projectId}/baselines`),
  createBaseline: (projectId: string, input: { name: string; baselineImportId: string; rules?: BaselineRule[] }) => request<BaselinePolicy>(`/api/projects/${projectId}/baselines`, { method: 'POST', body: JSON.stringify(input) }),
  evaluateBaseline: (projectId: string, policyId: string, importId: string) => request<BaselineEvaluation>(`/api/projects/${projectId}/baselines/${policyId}/evaluate/${importId}`, { method: 'POST' }),
  getAutomationRecipes: (projectId: string) => request<AutomationRecipe[]>(`/api/projects/${projectId}/automation-recipes`),
  createAutomationRecipe: (projectId: string, input: { name: string; description: string; steps: AutomationStep[]; triggerMode?: string; scheduleIntervalMinutes?: number }) => request<AutomationRecipe>(`/api/projects/${projectId}/automation-recipes`, { method: 'POST', body: JSON.stringify(input) }),
  updateAutomationRecipe: (projectId: string, recipeId: string, input: Partial<{ enabled: boolean; triggerMode: string; scheduleIntervalMinutes: number }>) => request<AutomationRecipe>(`/api/projects/${projectId}/automation-recipes/${recipeId}`, { method: 'PATCH', body: JSON.stringify(input) }),
  runAutomationRecipe: (projectId: string, recipeId: string, importId: string) => request<AutomationRecipe>(`/api/projects/${projectId}/automation-recipes/${recipeId}/run/${importId}`, { method: 'POST' }),
  askEvidence: (projectId: string, importId: string, question: string, maximumCitations = 6) => request<EvidenceAnswer>(`/api/projects/${projectId}/imports/${importId}/evidence-assistant/ask`, { method: 'POST', body: JSON.stringify({ question, maximumCitations }) }),
  decisionBriefUrl: (projectId: string, importId: string) => url(`/api/projects/${projectId}/imports/${importId}/decision-brief`),

  getQueuePolicies: (projectId: string) => request<QueuePolicy[]>(`/api/projects/${projectId}/queue-policies`),
  createQueuePolicy: (projectId: string, input: { name: string; weights: QueueWeight[] | null; slaHours: number; active: boolean }) => request<QueuePolicy>(`/api/projects/${projectId}/queue-policies`, { method: 'POST', body: JSON.stringify(input) }),
  updateQueuePolicy: (projectId: string, policyId: string, input: Partial<{ name: string; weights: QueueWeight[]; slaHours: number; active: boolean }>) => request<QueuePolicy>(`/api/projects/${projectId}/queue-policies/${policyId}`, { method: 'PATCH', body: JSON.stringify(input) }),
  getAdaptiveQueue: (projectId: string, importId: string, policyId?: string) => request<AdaptiveQueueItem[]>(`/api/projects/${projectId}/imports/${importId}/adaptive-queue${policyId ? `?policyId=${policyId}` : ''}`),
  getScenarios: (projectId: string) => request<ScenarioRun[]>(`/api/projects/${projectId}/scenarios`),
  runScenario: (projectId: string, input: { name: string; importId: string; assumptions: ScenarioAssumption[] }) => request<ScenarioRun>(`/api/projects/${projectId}/scenarios`, { method: 'POST', body: JSON.stringify(input) }),
  getApprovalGates: (projectId: string) => request<ApprovalGate[]>(`/api/projects/${projectId}/approval-gates`),
  createApprovalGate: (projectId: string, input: { name: string; importId: string; gateType: string; requiredRole: string }) => request<ApprovalGate>(`/api/projects/${projectId}/approval-gates`, { method: 'POST', body: JSON.stringify(input) }),
  decideApprovalGate: (projectId: string, gateId: string, input: { decision: 'Approve' | 'Reject'; decidedBy: string; rationale?: string }) => request<ApprovalGate>(`/api/projects/${projectId}/approval-gates/${gateId}/decision`, { method: 'POST', body: JSON.stringify(input) }),
  getAnomalyExplanations: (projectId: string, importId: string) => request<AnomalyExplanation[]>(`/api/projects/${projectId}/imports/${importId}/anomaly-explanations`),
  getExecutiveSummary: (projectId: string, importId: string) => request<ExecutiveSummary>(`/api/projects/${projectId}/imports/${importId}/executive-summary`),
  executiveBriefUrl: (projectId: string, importId: string) => url(`/api/projects/${projectId}/imports/${importId}/executive-brief`),
};
