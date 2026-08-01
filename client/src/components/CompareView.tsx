import { useEffect, useMemo, useState, type ChangeEvent } from 'react';
import { api } from '../lib/api';
import { formatBytes } from '../lib/format';
import type { CompareResult, ImportSummary } from '../lib/types';
import { MetricCard } from './MetricCard';
import { StatusBadge } from './StatusBadge';

interface CompareViewProps {
  projectId: string;
  imports: ImportSummary[];
}

export function CompareView({ projectId, imports }: CompareViewProps) {
  const completedImports = useMemo(
    () => imports.filter((item) => ['Completed', 'CompletedWithWarnings'].includes(item.status)),
    [imports],
  );
  const [leftId, setLeftId] = useState('');
  const [rightId, setRightId] = useState('');
  const [result, setResult] = useState<CompareResult | null>(null);
  const [filter, setFilter] = useState<'Changed' | 'All' | 'Added' | 'Removed' | 'Modified' | 'Unchanged'>('Changed');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!leftId && completedImports[1]) setLeftId(completedImports[1].id);
    if (!rightId && completedImports[0]) setRightId(completedImports[0].id);
  }, [completedImports, leftId, rightId]);

  const visible = useMemo(() => {
    if (!result) return [];
    const term = search.trim().toLowerCase();
    return result.differences.filter((item) => {
      if (filter === 'Changed' && item.changeType === 'Unchanged') return false;
      if (filter !== 'All' && filter !== 'Changed' && item.changeType !== filter) return false;
      if (term && !item.relativePath.toLowerCase().includes(term)) return false;
      return true;
    });
  }, [filter, result, search]);

  const changedCount = result ? result.addedCount + result.removedCount + result.modifiedCount : 0;
  const totalCount = result ? changedCount + result.unchangedCount : 0;
  const changePercent = totalCount === 0 ? 0 : Math.round((changedCount / totalCount) * 100);

  async function runComparison() {
    if (!leftId || !rightId || leftId === rightId) return;
    setLoading(true);
    setError(null);
    try {
      setResult(await api.compare(projectId, leftId, rightId));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Comparison failed.');
    } finally {
      setLoading(false);
    }
  }

  function swapSnapshots() {
    setLeftId(rightId);
    setRightId(leftId);
    setResult(null);
  }

  if (completedImports.length < 2) {
    return (
      <div className="empty-state large-empty">
        <div className="empty-icon" aria-hidden="true">⇄</div>
        <h2>Two completed snapshots are required</h2>
        <p>Import another version of the project to identify added, removed, modified, and unchanged artifacts.</p>
      </div>
    );
  }

  return (
    <div className="page-stack">
      <section className="surface-card compare-controls">
        <div className="section-heading">
          <div><span className="eyebrow">Snapshot comparison</span><h2>Compare immutable imports</h2></div>
          {result ? <span className="comparison-signal"><strong>{changePercent}%</strong><small>of paths changed</small></span> : null}
        </div>
        <div className="compare-picker-grid evolved-compare-grid">
          <label className="field-label">Baseline snapshot<select value={leftId} onChange={(event: ChangeEvent<HTMLSelectElement>) => { setLeftId(event.target.value); setResult(null); }}>{completedImports.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}</select></label>
          <button type="button" className="swap-button" aria-label="Swap baseline and new snapshot" title="Swap snapshots" onClick={swapSnapshots}>⇄</button>
          <label className="field-label">New snapshot<select value={rightId} onChange={(event: ChangeEvent<HTMLSelectElement>) => { setRightId(event.target.value); setResult(null); }}>{completedImports.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}</select></label>
          <button type="button" className="primary-button" disabled={loading || leftId === rightId} onClick={() => void runComparison()}>{loading ? 'Comparing…' : 'Run comparison'}</button>
        </div>
        {leftId === rightId ? <p className="validation-note">Choose two different snapshots.</p> : null}
        {error ? <div className="inline-error">{error}</div> : null}
      </section>

      {result ? (
        <>
          <section className="metrics-grid compare-metrics">
            <MetricCard label="Added" value={result.addedCount.toLocaleString()} tone="success" />
            <MetricCard label="Removed" value={result.removedCount.toLocaleString()} tone="danger" />
            <MetricCard label="Modified" value={result.modifiedCount.toLocaleString()} tone="warning" />
            <MetricCard label="Unchanged" value={result.unchangedCount.toLocaleString()} />
          </section>
          <section className="surface-card table-card compare-table-card">
            <div className="table-toolbar compare-toolbar">
              <div><strong>{visible.length.toLocaleString()} comparison rows</strong><span>Content identity is based on normalized relative paths and SHA-256.</span></div>
              <label className="search-field compare-search"><span aria-hidden="true">⌕</span><span className="sr-only">Search comparison paths</span><input value={search} onChange={(event: ChangeEvent<HTMLInputElement>) => setSearch(event.target.value)} placeholder="Filter paths…" />{search ? <button type="button" className="clear-input" aria-label="Clear comparison search" onClick={() => setSearch('')}>×</button> : null}</label>
              <label className="compact-select"><span className="sr-only">Filter comparison rows</span><select value={filter} onChange={(event: ChangeEvent<HTMLSelectElement>) => setFilter(event.target.value as typeof filter)}><option value="Changed">Changed only</option><option value="All">All rows</option><option value="Added">Added</option><option value="Removed">Removed</option><option value="Modified">Modified</option><option value="Unchanged">Unchanged</option></select></label>
            </div>
            <div className="table-scroll">
              <table className="data-table">
                <thead><tr><th>Change</th><th>Relative path</th><th>Baseline size</th><th>New size</th><th>Delta</th></tr></thead>
                <tbody>
                  {visible.map((item) => {
                    const delta = (item.rightSizeBytes ?? 0) - (item.leftSizeBytes ?? 0);
                    return <tr key={item.relativePath}><td><StatusBadge value={item.changeType} compact /></td><td className="path-cell">{item.relativePath}</td><td>{item.leftSizeBytes === null ? '—' : formatBytes(item.leftSizeBytes)}</td><td>{item.rightSizeBytes === null ? '—' : formatBytes(item.rightSizeBytes)}</td><td className={delta > 0 ? 'delta-positive' : delta < 0 ? 'delta-negative' : ''}>{delta === 0 ? '—' : `${delta > 0 ? '+' : '−'}${formatBytes(Math.abs(delta))}`}</td></tr>;
                  })}
                </tbody>
              </table>
              {visible.length === 0 ? <div className="empty-panel">No comparison rows match the current filters.</div> : null}
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}
