import type { CSSProperties } from 'react';
import { formatBytes, formatDate } from '../lib/format';
import { getDuplicateGroups } from '../lib/insights';
import type { ArtifactListItem, Finding, ImportSummary, ViewId, WorkspaceInsights as InsightModel } from '../lib/types';
import { StatusBadge } from './StatusBadge';

interface WorkspaceInsightsProps {
  insights: InsightModel;
  artifacts: ArtifactListItem[];
  findings: Finding[];
  imports: ImportSummary[];
  onNavigate: (view: ViewId) => void;
  onInspectArtifact: (artifactId: string) => void;
}

export function WorkspaceInsights({
  insights,
  artifacts,
  findings,
  imports,
  onNavigate,
  onInspectArtifact,
}: WorkspaceInsightsProps) {
  const duplicateGroups = getDuplicateGroups(artifacts).slice(0, 4);
  const priorityFindings = [...findings]
    .sort((left, right) => {
      const rank = (severity: string) => severity === 'Error' ? 3 : severity === 'Warning' ? 2 : 1;
      return rank(right.severity) - rank(left.severity) || right.createdAtUtc.localeCompare(left.createdAtUtc);
    })
    .slice(0, 4);
  const completedImports = imports.filter((item) => ['Completed', 'CompletedWithWarnings'].includes(item.status)).slice(0, 5);

  return (
    <section className="insights-layout" aria-label="Snapshot intelligence">
      <article className="surface-card health-card">
        <div className="section-heading compact-heading">
          <div><span className="eyebrow">Readiness signal</span><h2>Workspace health</h2></div>
          <span className="quiet-label">Heuristic, not a release gate</span>
        </div>
        <div className="health-body">
          <div className="health-gauge" style={{ '--health-value': `${insights.healthScore}%` } as CSSProperties}>
            <div><strong>{insights.healthScore}</strong><span>/ 100</span></div>
          </div>
          <div className="health-copy">
            <strong>{insights.healthLabel}</strong>
            <p>Based on parse coverage, unsupported formats, parser failures, and current validation severity.</p>
            <dl className="mini-metrics">
              <div><dt>Coverage</dt><dd>{insights.parseCoveragePercent}%</dd></div>
              <div><dt>Parser failures</dt><dd>{insights.failedCount}</dd></div>
              <div><dt>Duplicate groups</dt><dd>{insights.duplicateGroupCount}</dd></div>
            </dl>
          </div>
        </div>
      </article>

      <article className="surface-card distribution-card">
        <div className="section-heading compact-heading">
          <div><span className="eyebrow">Composition</span><h2>Artifact types</h2></div>
          <button type="button" className="text-button" onClick={() => onNavigate('inventory')}>Open inventory</button>
        </div>
        {insights.typeDistribution.length > 0 ? (
          <div className="distribution-list">
            {insights.typeDistribution.map((item) => (
              <div key={item.label} className="distribution-row">
                <span className="distribution-label"><strong>{item.label}</strong><small>{item.count.toLocaleString()} · {formatBytes(item.bytes)}</small></span>
                <span className="distribution-track" aria-label={`${item.label}: ${item.percent}%`}><span style={{ width: `${Math.max(item.percent, 2)}%` }} /></span>
                <strong>{item.percent}%</strong>
              </div>
            ))}
          </div>
        ) : <div className="empty-panel">Import a snapshot to profile artifact composition.</div>}
      </article>

      <article className="surface-card priority-card">
        <div className="section-heading compact-heading">
          <div><span className="eyebrow">Triage</span><h2>Priority findings</h2></div>
          <button type="button" className="text-button" onClick={() => onNavigate('findings')}>Review all</button>
        </div>
        {priorityFindings.length > 0 ? (
          <div className="priority-list">
            {priorityFindings.map((finding) => (
              <button
                key={finding.id}
                type="button"
                className="priority-row"
                onClick={() => finding.artifactId ? onInspectArtifact(finding.artifactId) : onNavigate('findings')}
              >
                <StatusBadge value={finding.severity} compact />
                <span><strong>{finding.title}</strong><small>{finding.artifactPath ?? 'Project-level finding'}</small></span>
                <code>{finding.ruleId}</code>
              </button>
            ))}
          </div>
        ) : <div className="empty-panel success-empty">No validation findings require attention.</div>}
      </article>

      <article className="surface-card duplicate-card">
        <div className="section-heading compact-heading">
          <div><span className="eyebrow">Content identity</span><h2>Duplicate groups</h2></div>
          <span className="quiet-label">SHA-256</span>
        </div>
        {duplicateGroups.length > 0 ? (
          <div className="duplicate-list">
            {duplicateGroups.map((group) => (
              <div key={group.sha256} className="duplicate-row">
                <div><strong>{group.items.length} identical artifacts</strong><code>{group.sha256.slice(0, 16)}…</code></div>
                <button type="button" className="text-button" onClick={() => onInspectArtifact(group.items[0]!.id)}>Inspect</button>
              </div>
            ))}
          </div>
        ) : <div className="empty-panel">No duplicate content hashes were detected.</div>}
      </article>

      <article className="surface-card history-card">
        <div className="section-heading compact-heading">
          <div><span className="eyebrow">Continuity</span><h2>Recent completed snapshots</h2></div>
          {completedImports.length >= 2 ? <button type="button" className="text-button" onClick={() => onNavigate('compare')}>Compare</button> : null}
        </div>
        {completedImports.length > 0 ? (
          <ol className="timeline-list">
            {completedImports.map((item, index) => (
              <li key={item.id}>
                <span className="timeline-marker" aria-hidden="true">{index + 1}</span>
                <span><strong>{item.displayName}</strong><small>{formatDate(item.completedAtUtc ?? item.createdAtUtc)} · {item.totalFiles.toLocaleString()} artifacts</small></span>
                <StatusBadge value={item.status} compact />
              </li>
            ))}
          </ol>
        ) : <div className="empty-panel">No completed snapshots are available.</div>}
      </article>
    </section>
  );
}
