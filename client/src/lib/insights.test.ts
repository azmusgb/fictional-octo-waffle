import { describe, expect, it } from 'vitest';
import { buildArtifactTree, calculateWorkspaceInsights } from './insights';
import type { ArtifactListItem, Finding } from './types';

const artifact = (overrides: Partial<ArtifactListItem>): ArtifactListItem => ({
  id: crypto.randomUUID(),
  importSnapshotId: 'import',
  parentArtifactId: null,
  name: 'file.json',
  relativePath: 'folder/file.json',
  extension: '.json',
  mediaType: 'application/json',
  sizeBytes: 100,
  sha256: 'a'.repeat(64),
  parseStatus: 'Parsed',
  parserId: 'builtin.json',
  parserVersion: '1.0.0',
  importedAtUtc: '2026-08-01T00:00:00Z',
  findingCount: 0,
  reviewStatus: 'Unreviewed',
  reviewUpdatedAtUtc: null,
  ...overrides,
});

const finding = (severity: Finding['severity']): Finding => ({
  id: crypto.randomUUID(),
  importSnapshotId: 'import',
  artifactId: null,
  artifactPath: null,
  severity,
  ruleId: 'RULE',
  title: 'Finding',
  message: 'Message',
  sourceLocation: null,
  evidenceExcerpt: null,
  recommendation: null,
  createdAtUtc: '2026-08-01T00:00:00Z',
});

describe('calculateWorkspaceInsights', () => {
  it('calculates coverage and duplicate groups', () => {
    const artifacts = [
      artifact({ id: '1', sha256: 'x', relativePath: 'a/file.json' }),
      artifact({ id: '2', sha256: 'x', relativePath: 'b/file.json', parseStatus: 'ParsedWithWarnings' }),
      artifact({ id: '3', sha256: 'y', relativePath: 'c/file.pdf', extension: '.pdf', parseStatus: 'Unsupported' }),
    ];
    const result = calculateWorkspaceInsights(artifacts, [finding('Warning')]);
    expect(result.parseCoveragePercent).toBe(67);
    expect(result.duplicateGroupCount).toBe(1);
    expect(result.duplicateArtifactCount).toBe(2);
    expect(result.typeDistribution[0]?.label).toBe('JSON');
  });
});

describe('buildArtifactTree', () => {
  it('rolls up sizes and findings through folders', () => {
    const tree = buildArtifactTree([
      artifact({ id: '1', relativePath: 'root/a.json', sizeBytes: 10, findingCount: 1 }),
      artifact({ id: '2', relativePath: 'root/nested/b.json', sizeBytes: 20, findingCount: 2 }),
    ]);
    expect(tree).toHaveLength(1);
    expect(tree[0]?.aggregateSize).toBe(30);
    expect(tree[0]?.findingCount).toBe(3);
  });
});
