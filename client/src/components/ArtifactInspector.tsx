import { useEffect, useMemo, useState, type ChangeEvent } from 'react';
import { api } from '../lib/api';
import { formatBytes, formatDate } from '../lib/format';
import type { ArtifactDetail, ArtifactReview, ReviewStatus } from '../lib/types';
import { StatusBadge } from './StatusBadge';

interface ArtifactInspectorProps {
  artifactId: string | null;
  onClose: () => void;
  onToast?: (message: string) => void;
  onReviewUpdated?: (artifactId: string, review: ArtifactReview) => void;
}

type TabId = 'overview' | 'structure' | 'preview' | 'findings' | 'review';

const reviewOptions: Array<{ value: ReviewStatus; label: string; description: string }> = [
  { value: 'Unreviewed', label: 'Unreviewed', description: 'No human decision has been recorded.' },
  { value: 'InReview', label: 'In review', description: 'Evidence is actively being evaluated.' },
  { value: 'NeedsAttention', label: 'Needs attention', description: 'Follow-up or correction is required.' },
  { value: 'Accepted', label: 'Accepted', description: 'Evidence has been reviewed and accepted.' },
];

export function ArtifactInspector({ artifactId, onClose, onToast, onReviewUpdated }: ArtifactInspectorProps) {
  const [detail, setDetail] = useState<ArtifactDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<TabId>('overview');
  const [reviewStatus, setReviewStatus] = useState<ReviewStatus>('Unreviewed');
  const [reviewNote, setReviewNote] = useState('');
  const [reviewTags, setReviewTags] = useState('');
  const [reviewSaving, setReviewSaving] = useState(false);

  useEffect(() => {
    if (!artifactId) {
      setDetail(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);
    setTab('overview');
    void api.getArtifact(artifactId)
      .then((result) => {
        if (cancelled) return;
        setDetail(result);
        setReviewStatus(result.review.status);
        setReviewNote(result.review.note ?? '');
        setReviewTags(result.review.tags.join(', '));
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'Could not load artifact details.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [artifactId]);

  const structureEntries = useMemo(() => {
    if (!detail?.structureSummary || typeof detail.structureSummary !== 'object' || Array.isArray(detail.structureSummary)) return [];
    return Object.entries(detail.structureSummary as Record<string, unknown>)
      .filter(([, value]) => ['string', 'number', 'boolean'].includes(typeof value) || value === null)
      .slice(0, 12);
  }, [detail]);

  const reviewDirty = useMemo(() => {
    if (!detail) return false;
    const normalizedTags = normalizeTags(reviewTags);
    return reviewStatus !== detail.review.status ||
      reviewNote.trim() !== (detail.review.note ?? '') ||
      normalizedTags.join('\n') !== detail.review.tags.join('\n');
  }, [detail, reviewNote, reviewStatus, reviewTags]);

  if (!artifactId) return null;

  async function copy(value: string, label: string) {
    try {
      await navigator.clipboard.writeText(value);
      onToast?.(`${label} copied.`);
    } catch {
      onToast?.(`Could not copy ${label.toLowerCase()}.`);
    }
  }

  async function saveReview() {
    if (!detail || !reviewDirty) return;
    setReviewSaving(true);
    setError(null);
    try {
      const review = await api.updateArtifactReview(
        detail.artifact.id,
        reviewStatus,
        reviewNote.trim(),
        normalizeTags(reviewTags),
      );
      setDetail((current) => current ? {
        ...current,
        artifact: {
          ...current.artifact,
          reviewStatus: review.status,
          reviewUpdatedAtUtc: review.updatedAtUtc,
        },
        review,
      } : current);
      setReviewNote(review.note ?? '');
      setReviewTags(review.tags.join(', '));
      onReviewUpdated?.(detail.artifact.id, review);
      onToast?.('Artifact review saved.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not save the artifact review.');
    } finally {
      setReviewSaving(false);
    }
  }

  return (
    <aside className="inspector" aria-label="Artifact inspector">
      <div className="inspector-header">
        <div>
          <span className="eyebrow">Evidence inspector</span>
          <h2 title={detail?.artifact.relativePath}>{detail?.artifact.name ?? 'Loading artifact'}</h2>
        </div>
        <button type="button" className="icon-button" aria-label="Close artifact inspector" onClick={onClose}>×</button>
      </div>

      {loading ? <div className="panel-loading">Loading artifact evidence…</div> : null}
      {error ? <div className="inline-error" role="alert">{error}</div> : null}

      {detail ? (
        <>
          <div className="inspector-summary">
            <StatusBadge value={detail.artifact.parseStatus} compact />
            <StatusBadge value={detail.artifact.reviewStatus} compact />
            <span>{formatBytes(detail.artifact.sizeBytes)}</span>
            <span>{detail.artifact.extension || 'No extension'}</span>
          </div>
          <div className="inspector-actions">
            <a className="secondary-button compact-button" href={api.artifactContentUrl(detail.artifact.id)} download={detail.artifact.name}>Download original</a>
            <button type="button" className="secondary-button compact-button" onClick={() => void copy(detail.artifact.relativePath, 'Path')}>Copy path</button>
          </div>
          <div className="tab-strip" role="tablist" aria-label="Artifact details">
            {(['overview', 'structure', 'preview', 'findings', 'review'] as const).map((item) => (
              <button
                key={item}
                type="button"
                role="tab"
                aria-selected={tab === item}
                className={tab === item ? 'is-active' : ''}
                onClick={() => setTab(item)}
              >
                {item[0]?.toUpperCase()}{item.slice(1)}
                {item === 'findings' && detail.findings.length > 0 ? ` (${detail.findings.length})` : ''}
              </button>
            ))}
          </div>

          <div className="inspector-body">
            {tab === 'overview' ? (
              <>
                {detail.parseError ? <div className="parse-error"><strong>Parser error</strong><span>{detail.parseError}</span></div> : null}
                <dl className="property-list">
                  <div><dt>Relative path</dt><dd>{detail.artifact.relativePath}</dd></div>
                  <div><dt>Media type</dt><dd>{detail.artifact.mediaType}</dd></div>
                  <div><dt>Parser</dt><dd>{detail.artifact.parserId ?? 'Not supported'}{detail.artifact.parserVersion ? ` · ${detail.artifact.parserVersion}` : ''}</dd></div>
                  <div><dt>Imported</dt><dd>{formatDate(detail.artifact.importedAtUtc)}</dd></div>
                  <div><dt>Review state</dt><dd><StatusBadge value={detail.artifact.reviewStatus} compact /></dd></div>
                  <div><dt>Reviewed</dt><dd>{detail.artifact.reviewUpdatedAtUtc ? formatDate(detail.artifact.reviewUpdatedAtUtc) : 'Not yet reviewed'}</dd></div>
                  <div><dt>Parent artifact</dt><dd>{detail.artifact.parentArtifactId ?? 'None'}</dd></div>
                  <div className="hash-property"><dt>SHA-256</dt><dd><code>{detail.artifact.sha256}</code><button type="button" className="text-button" onClick={() => void copy(detail.artifact.sha256, 'SHA-256')}>Copy</button></dd></div>
                </dl>
                <div className="provenance-callout"><strong>Provenance preserved</strong><p>This result remains bound to the immutable snapshot, stored artifact, content hash, parser identity, and evidence locations shown in its findings. Review annotations are stored separately from source evidence.</p></div>
              </>
            ) : null}

            {tab === 'structure' ? (
              detail.structureSummary ? (
                <div className="structure-stack">
                  {structureEntries.length > 0 ? <dl className="structure-summary-grid">{structureEntries.map(([key, value]) => <div key={key}><dt>{key.replace(/([a-z])([A-Z])/g, '$1 $2')}</dt><dd>{value === null ? '—' : String(value)}</dd></div>)}</dl> : null}
                  <details className="raw-structure" open={structureEntries.length === 0}><summary>Raw parser summary</summary><pre className="code-panel">{JSON.stringify(detail.structureSummary, null, 2)}</pre></details>
                </div>
              ) : (
                <div className="empty-panel">No structured parser summary is available.</div>
              )
            ) : null}

            {tab === 'preview' ? (
              detail.previewText ? (
                <CodePreview value={detail.previewText} />
              ) : (
                <div className="empty-panel">No safe text preview is available for this artifact.</div>
              )
            ) : null}

            {tab === 'findings' ? (
              detail.findings.length > 0 ? (
                <div className="inspector-findings">
                  {detail.findings.map((finding) => (
                    <article key={finding.id} className="finding-card compact-finding">
                      <div className="finding-title-row">
                        <StatusBadge value={finding.severity} compact />
                        <code>{finding.ruleId}</code>
                      </div>
                      <strong>{finding.title}</strong>
                      <p>{finding.message}</p>
                      {finding.sourceLocation ? <small>{finding.sourceLocation}</small> : null}
                      {finding.evidenceExcerpt ? <blockquote>{finding.evidenceExcerpt}</blockquote> : null}
                      {finding.recommendation ? <p className="recommendation">{finding.recommendation}</p> : null}
                    </article>
                  ))}
                </div>
              ) : (
                <div className="empty-panel success-empty">No findings are associated with this artifact.</div>
              )
            ) : null}

            {tab === 'review' ? (
              <div className="review-editor">
                <div className="review-editor-intro">
                  <span className="eyebrow">Human annotation</span>
                  <h3>Record the review decision</h3>
                  <p>Review state, notes, and tags are snapshot-bound annotations. They do not modify the imported artifact or parser evidence.</p>
                </div>
                <fieldset className="review-status-options">
                  <legend>Review status</legend>
                  {reviewOptions.map((option) => (
                    <label key={option.value} className={reviewStatus === option.value ? 'review-option is-selected' : 'review-option'}>
                      <input type="radio" name="review-status" value={option.value} checked={reviewStatus === option.value} onChange={() => setReviewStatus(option.value)} />
                      <span><strong>{option.label}</strong><small>{option.description}</small></span>
                    </label>
                  ))}
                </fieldset>
                <label className="field-label">Review note<textarea value={reviewNote} onChange={(event: ChangeEvent<HTMLTextAreaElement>) => setReviewNote(event.target.value)} maxLength={4000} rows={7} placeholder="Capture the decision, required follow-up, or evidence rationale." /><small>{reviewNote.length.toLocaleString()} / 4,000 characters</small></label>
                <label className="field-label">Tags<input value={reviewTags} onChange={(event: ChangeEvent<HTMLInputElement>) => setReviewTags(event.target.value)} placeholder="schema, production, needs-owner" /><small>Comma-separated; up to 20 tags, 40 characters each.</small></label>
                <div className="review-save-row">
                  <span>{detail.review.updatedAtUtc ? `Last saved ${formatDate(detail.review.updatedAtUtc)}` : 'No review saved yet'}</span>
                  <button type="button" className="primary-button" onClick={() => void saveReview()} disabled={!reviewDirty || reviewSaving}>{reviewSaving ? 'Saving…' : 'Save review'}</button>
                </div>
              </div>
            ) : null}
          </div>
        </>
      ) : null}
    </aside>
  );
}

function normalizeTags(value: string): string[] {
  return value
    .split(',')
    .map((tag) => tag.trim())
    .filter(Boolean)
    .filter((tag, index, all) => all.findIndex((candidate) => candidate.toLowerCase() === tag.toLowerCase()) === index)
    .slice(0, 20)
    .map((tag) => tag.slice(0, 40));
}

function CodePreview({ value }: { value: string }) {
  const lines = value.split('\n');
  return (
    <div className="line-preview" role="region" aria-label="Artifact text preview">
      {lines.map((line, index) => (
        <div key={`${index}:${line.slice(0, 20)}`} className="preview-line"><span>{index + 1}</span><code>{line || ' '}</code></div>
      ))}
    </div>
  );
}
