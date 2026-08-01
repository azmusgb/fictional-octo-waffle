import type { ArtifactListItem, ArtifactTreeNode, Finding, ReviewStatus, WorkspaceInsights } from './types';

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value));
}

function extensionLabel(artifact: ArtifactListItem) {
  if (artifact.extension) return artifact.extension.slice(1).toUpperCase();
  const mediaLeaf = artifact.mediaType.split('/').at(-1);
  return mediaLeaf?.toUpperCase() || 'FILE';
}

export function calculateWorkspaceInsights(
  artifacts: ArtifactListItem[],
  findings: Finding[],
): WorkspaceInsights {
  const artifactCount = artifacts.length;
  const parsedCount = artifacts.filter((item) => ['Parsed', 'ParsedWithWarnings'].includes(item.parseStatus)).length;
  const unsupportedCount = artifacts.filter((item) => item.parseStatus === 'Unsupported').length;
  const failedCount = artifacts.filter((item) => item.parseStatus === 'Failed').length;
  const totalBytes = artifacts.reduce((total, item) => total + item.sizeBytes, 0);

  const duplicateHashes = new Map<string, number>();
  for (const artifact of artifacts) {
    duplicateHashes.set(artifact.sha256, (duplicateHashes.get(artifact.sha256) ?? 0) + 1);
  }
  const duplicateGroups = [...duplicateHashes.values()].filter((count) => count > 1);
  const duplicateArtifactCount = duplicateGroups.reduce((total, count) => total + count, 0);

  const errorCount = findings.filter((item) => item.severity === 'Error').length;
  const warningCount = findings.filter((item) => item.severity === 'Warning').length;
  const parseCoveragePercent = artifactCount === 0 ? 0 : Math.round((parsedCount / artifactCount) * 100);
  const unsupportedRatio = artifactCount === 0 ? 0 : unsupportedCount / artifactCount;
  const failedRatio = artifactCount === 0 ? 0 : failedCount / artifactCount;
  const issuePenalty = Math.min(45, errorCount * 8 + warningCount * 1.5);
  const coveragePenalty = (1 - parseCoveragePercent / 100) * 22;
  const supportPenalty = unsupportedRatio * 16;
  const failurePenalty = failedRatio * 28;
  const healthScore = artifactCount === 0
    ? 0
    : clamp(Math.round(100 - issuePenalty - coveragePenalty - supportPenalty - failurePenalty), 0, 100);
  const healthLabel = healthScore >= 90 ? 'Excellent' : healthScore >= 75 ? 'Healthy' : healthScore >= 55 ? 'Needs review' : 'At risk';

  const typeMap = new Map<string, { count: number; bytes: number }>();
  for (const artifact of artifacts) {
    const label = extensionLabel(artifact);
    const current = typeMap.get(label) ?? { count: 0, bytes: 0 };
    current.count += 1;
    current.bytes += artifact.sizeBytes;
    typeMap.set(label, current);
  }
  const typeDistribution = [...typeMap.entries()]
    .map(([label, value]) => ({
      label,
      count: value.count,
      bytes: value.bytes,
      percent: artifactCount === 0 ? 0 : Math.round((value.count / artifactCount) * 100),
    }))
    .sort((left, right) => right.count - left.count || left.label.localeCompare(right.label))
    .slice(0, 8);

  const statusMap = new Map<string, number>();
  for (const artifact of artifacts) {
    statusMap.set(artifact.parseStatus, (statusMap.get(artifact.parseStatus) ?? 0) + 1);
  }
  const statusDistribution = [...statusMap.entries()]
    .map(([label, count]) => ({
      label,
      count,
      percent: artifactCount === 0 ? 0 : Math.round((count / artifactCount) * 100),
    }))
    .sort((left, right) => right.count - left.count);

  return {
    artifactCount,
    parsedCount,
    unsupportedCount,
    failedCount,
    duplicateArtifactCount,
    duplicateGroupCount: duplicateGroups.length,
    totalBytes,
    parseCoveragePercent,
    healthScore,
    healthLabel,
    typeDistribution,
    statusDistribution,
  };
}

export function buildArtifactTree(artifacts: ArtifactListItem[]): ArtifactTreeNode[] {
  const root: ArtifactTreeNode = {
    id: 'root',
    name: 'root',
    path: '',
    kind: 'folder',
    children: [],
    aggregateSize: 0,
    findingCount: 0,
    reviewStatus: 'Unreviewed',
    reviewUpdatedAtUtc: null,
  };

  for (const artifact of artifacts) {
    const parts = artifact.relativePath.split('/').filter(Boolean);
    let current = root;
    for (let index = 0; index < parts.length; index++) {
      const part = parts[index]!;
      const isArtifact = index === parts.length - 1;
      const nodePath = parts.slice(0, index + 1).join('/');
      let child = current.children.find((item) => item.name === part && item.kind === (isArtifact ? 'artifact' : 'folder'));
      if (!child) {
        child = {
          id: isArtifact ? artifact.id : `folder:${nodePath}`,
          name: part,
          path: nodePath,
          kind: isArtifact ? 'artifact' : 'folder',
          children: [],
          artifact: isArtifact ? artifact : undefined,
          aggregateSize: isArtifact ? artifact.sizeBytes : 0,
          findingCount: isArtifact ? artifact.findingCount : 0,
          reviewStatus: isArtifact ? artifact.reviewStatus : 'Unreviewed',
          reviewUpdatedAtUtc: isArtifact ? artifact.reviewUpdatedAtUtc : null,
        };
        current.children.push(child);
      }
      current = child;
    }
  }

  function rollup(node: ArtifactTreeNode): ArtifactTreeNode {
    node.children = node.children
      .map(rollup)
      .sort((left, right) => {
        if (left.kind !== right.kind) return left.kind === 'folder' ? -1 : 1;
        return left.name.localeCompare(right.name, undefined, { numeric: true, sensitivity: 'base' });
      });
    if (node.kind === 'folder') {
      node.aggregateSize = node.children.reduce((total, child) => total + child.aggregateSize, 0);
      node.findingCount = node.children.reduce((total, child) => total + child.findingCount, 0);
      node.reviewStatus = aggregateReviewStatus(node.children.map((child) => child.reviewStatus));
      node.reviewUpdatedAtUtc = latestReviewTimestamp(node.children.map((child) => child.reviewUpdatedAtUtc));
    }
    return node;
  }

  return rollup(root).children;
}


function aggregateReviewStatus(statuses: ReviewStatus[]): ReviewStatus {
  if (statuses.includes('NeedsAttention')) return 'NeedsAttention';
  if (statuses.includes('InReview')) return 'InReview';
  if (statuses.includes('Unreviewed')) return 'Unreviewed';
  return statuses.length > 0 ? 'Accepted' : 'Unreviewed';
}

function latestReviewTimestamp(values: Array<string | null>): string | null {
  const timestamps = values.filter((value): value is string => Boolean(value));
  if (timestamps.length === 0) return null;
  return timestamps.sort((left, right) => right.localeCompare(left))[0] ?? null;
}

export function getDuplicateGroups(artifacts: ArtifactListItem[]) {
  const groups = new Map<string, ArtifactListItem[]>();
  for (const artifact of artifacts) {
    const current = groups.get(artifact.sha256) ?? [];
    current.push(artifact);
    groups.set(artifact.sha256, current);
  }
  return [...groups.entries()]
    .filter(([, items]) => items.length > 1)
    .map(([sha256, items]) => ({ sha256, items: [...items].sort((a, b) => a.relativePath.localeCompare(b.relativePath)) }))
    .sort((left, right) => right.items.length - left.items.length);
}
