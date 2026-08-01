import { useMemo, useState, type ChangeEvent } from 'react';
import type { ArtifactListItem, ReviewStatus } from '../lib/types';
import { formatBytes, formatDate } from '../lib/format';
import { StatusBadge } from './StatusBadge';

const statuses: Array<{ value: 'All' | ReviewStatus; label: string }> = [
  { value: 'All', label: 'All' },
  { value: 'Unreviewed', label: 'Unreviewed' },
  { value: 'InReview', label: 'In review' },
  { value: 'NeedsAttention', label: 'Needs attention' },
  { value: 'Accepted', label: 'Accepted' },
];

export function ReviewQueue({ artifacts, onInspectArtifact }: {
  artifacts: ArtifactListItem[];
  onInspectArtifact: (artifactId: string) => void;
}) {
  const [status, setStatus] = useState<'All' | ReviewStatus>('All');
  const [query, setQuery] = useState('');
  const rows = useMemo(() => {
    const term = query.trim().toLowerCase();
    return artifacts.filter((artifact) => {
      if (status !== 'All' && artifact.reviewStatus !== status) return false;
      return !term || [artifact.relativePath, artifact.parserId ?? '', artifact.mediaType]
        .some((value) => value.toLowerCase().includes(term));
    }).sort((left, right) => {
      const priority: Record<ReviewStatus, number> = { NeedsAttention: 0, InReview: 1, Unreviewed: 2, Accepted: 3 };
      return priority[left.reviewStatus] - priority[right.reviewStatus] || right.findingCount - left.findingCount || left.relativePath.localeCompare(right.relativePath);
    });
  }, [artifacts, query, status]);

  const counts = useMemo(() => Object.fromEntries(statuses.slice(1).map((item) => [item.value, artifacts.filter((artifact) => artifact.reviewStatus === item.value).length])), [artifacts]);

  return (
    <div className="page-stack">
      <section className="review-summary-grid">
        {statuses.slice(1).map((item) => (
          <button key={item.value} type="button" className={`review-summary review-${item.value.toLowerCase()}${status === item.value ? ' is-active' : ''}`} onClick={() => setStatus(status === item.value ? 'All' : item.value)}>
            <span>{item.label}</span><strong>{counts[item.value] ?? 0}</strong>
          </button>
        ))}
      </section>
      <section className="surface-card review-toolbar">
        <div><strong>{rows.length.toLocaleString()} artifacts</strong><span>Review state is local, snapshot-bound, and included in project manifests.</span></div>
        <div className="review-toolbar-controls">
          <label className="field-label compact-field">Status<select value={status} onChange={(event: ChangeEvent<HTMLSelectElement>) => setStatus(event.target.value as 'All' | ReviewStatus)}>{statuses.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
          <label className="search-field expanded-search"><span aria-hidden="true">⌕</span><span className="sr-only">Search review queue</span><input value={query} onChange={(event: ChangeEvent<HTMLInputElement>) => setQuery(event.target.value)} placeholder="Search review queue…" /></label>
        </div>
      </section>
      <section className="review-list">
        {rows.length ? rows.map((artifact) => (
          <button key={artifact.id} type="button" className="review-row" onClick={() => onInspectArtifact(artifact.id)}>
            <span className="review-row-main"><strong>{artifact.name}</strong><small>{artifact.relativePath}</small></span>
            <span className="review-row-meta"><StatusBadge value={artifact.reviewStatus} compact /><span>{artifact.findingCount} findings</span><span>{formatBytes(artifact.sizeBytes)}</span><span>{artifact.reviewUpdatedAtUtc ? formatDate(artifact.reviewUpdatedAtUtc) : 'Not reviewed'}</span></span>
          </button>
        )) : <div className="empty-state large-empty"><h2>No artifacts match</h2><p>Change the review status or search filter.</p></div>}
      </section>
    </div>
  );
}
