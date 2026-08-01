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
};
