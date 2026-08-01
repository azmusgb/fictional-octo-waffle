export type ViewId = 'overview' | 'inventory' | 'review' | 'findings' | 'compare' | 'exports' | 'system';
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
