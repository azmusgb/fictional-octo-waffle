import { useMemo, useState, type CSSProperties, type ChangeEvent, type KeyboardEvent as ReactKeyboardEvent } from 'react';
import { formatBytes } from '../lib/format';
import { buildArtifactTree } from '../lib/insights';
import type { ArtifactListItem, ArtifactSort, ArtifactTreeNode, InventoryViewMode } from '../lib/types';
import { StatusBadge } from './StatusBadge';

interface InventoryExplorerProps {
  artifacts: ArtifactListItem[];
  onSelectArtifact: (artifactId: string) => void;
}

export function InventoryExplorer({ artifacts, onSelectArtifact }: InventoryExplorerProps) {
  const [search, setSearch] = useState('');
  const [extension, setExtension] = useState('All');
  const [status, setStatus] = useState('All');
  const [findingsOnly, setFindingsOnly] = useState(false);
  const [viewMode, setViewMode] = useState<InventoryViewMode>('table');
  const [sort, setSort] = useState<ArtifactSort>('path');

  const extensions = useMemo(
    () => [...new Set(artifacts.map((item) => item.extension || '(none)'))].sort(),
    [artifacts],
  );
  const statuses = useMemo(
    () => [...new Set(artifacts.map((item) => item.parseStatus))].sort(),
    [artifacts],
  );

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    const rows = artifacts.filter((artifact) => {
      if (term && ![
        artifact.relativePath,
        artifact.sha256,
        artifact.parseStatus,
        artifact.parserId ?? '',
        artifact.mediaType,
      ].some((value) => value.toLowerCase().includes(term))) return false;
      if (extension !== 'All' && (artifact.extension || '(none)') !== extension) return false;
      if (status !== 'All' && artifact.parseStatus !== status) return false;
      if (findingsOnly && artifact.findingCount === 0) return false;
      return true;
    });

    return [...rows].sort((left, right) => {
      if (sort === 'size-desc') return right.sizeBytes - left.sizeBytes || left.relativePath.localeCompare(right.relativePath);
      if (sort === 'findings-desc') return right.findingCount - left.findingCount || left.relativePath.localeCompare(right.relativePath);
      if (sort === 'status') return left.parseStatus.localeCompare(right.parseStatus) || left.relativePath.localeCompare(right.relativePath);
      return left.relativePath.localeCompare(right.relativePath, undefined, { numeric: true, sensitivity: 'base' });
    });
  }, [artifacts, extension, findingsOnly, search, sort, status]);

  const tree = useMemo(() => buildArtifactTree(visible), [visible]);
  const hasFilters = search.trim() || extension !== 'All' || status !== 'All' || findingsOnly;

  function clearFilters() {
    setSearch('');
    setExtension('All');
    setStatus('All');
    setFindingsOnly(false);
  }

  return (
    <section className="surface-card inventory-explorer">
      <div className="inventory-toolbar">
        <div className="inventory-title">
          <strong>{visible.length.toLocaleString()} of {artifacts.length.toLocaleString()} artifacts</strong>
          <span>Filter, sort, explore the file tree, and open evidence without changing the snapshot.</span>
        </div>
        <label className="search-field expanded-search">
          <span aria-hidden="true">⌕</span>
          <span className="sr-only">Search artifact inventory</span>
          <input value={search} onChange={(event: ChangeEvent<HTMLInputElement>) => setSearch(event.target.value)} placeholder="Search path, hash, parser, type…" />
          {search ? <button type="button" className="clear-input" aria-label="Clear search" onClick={() => setSearch('')}>×</button> : null}
        </label>
      </div>

      <div className="filter-bar" aria-label="Inventory filters">
        <label className="compact-select"><span>Type</span><select value={extension} onChange={(event: ChangeEvent<HTMLSelectElement>) => setExtension(event.target.value)}><option value="All">All types</option>{extensions.map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
        <label className="compact-select"><span>Status</span><select value={status} onChange={(event: ChangeEvent<HTMLSelectElement>) => setStatus(event.target.value)}><option value="All">All statuses</option>{statuses.map((item) => <option key={item} value={item}>{item.replace(/([a-z])([A-Z])/g, '$1 $2')}</option>)}</select></label>
        <label className="compact-select"><span>Sort</span><select value={sort} onChange={(event: ChangeEvent<HTMLSelectElement>) => setSort(event.target.value as ArtifactSort)}><option value="path">Path</option><option value="size-desc">Largest first</option><option value="findings-desc">Most findings</option><option value="status">Parse status</option></select></label>
        <label className="check-filter"><input type="checkbox" checked={findingsOnly} onChange={(event: ChangeEvent<HTMLInputElement>) => setFindingsOnly(event.target.checked)} /><span>Findings only</span></label>
        {hasFilters ? <button type="button" className="text-button" onClick={clearFilters}>Clear filters</button> : <span className="filter-spacer" />}
        <div className="segmented-control" aria-label="Inventory presentation">
          <button type="button" className={viewMode === 'table' ? 'is-active' : ''} aria-pressed={viewMode === 'table'} onClick={() => setViewMode('table')}>Table</button>
          <button type="button" className={viewMode === 'tree' ? 'is-active' : ''} aria-pressed={viewMode === 'tree'} onClick={() => setViewMode('tree')}>Tree</button>
        </div>
      </div>

      {visible.length === 0 ? (
        <div className="empty-state large-empty"><div className="empty-icon" aria-hidden="true">⌕</div><h2>No matching artifacts</h2><p>Change the search or remove one of the active filters.</p><button type="button" className="secondary-button" onClick={clearFilters}>Reset inventory filters</button></div>
      ) : viewMode === 'table' ? (
        <div className="table-scroll inventory-table-scroll">
          <table className="data-table interactive-table">
            <thead><tr><th>Artifact</th><th>Type</th><th>Size</th><th>Parser</th><th>Status</th><th>Findings</th><th>SHA-256</th></tr></thead>
            <tbody>
              {visible.map((artifact) => (
                <tr key={artifact.id} tabIndex={0} onClick={() => onSelectArtifact(artifact.id)} onKeyDown={(event: ReactKeyboardEvent<HTMLTableRowElement>) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onSelectArtifact(artifact.id); } }}>
                  <td className="artifact-cell"><span className="file-glyph" aria-hidden="true">{artifact.extension ? artifact.extension.slice(1, 5).toUpperCase() : 'FILE'}</span><span><strong>{artifact.name}</strong><small>{artifact.relativePath}</small></span></td>
                  <td>{artifact.mediaType}</td>
                  <td>{formatBytes(artifact.sizeBytes)}</td>
                  <td>{artifact.parserId ? <span className="parser-label"><strong>{artifact.parserId.replace('builtin.', '')}</strong><small>{artifact.parserVersion ?? 'version unknown'}</small></span> : '—'}</td>
                  <td><StatusBadge value={artifact.parseStatus} compact /></td>
                  <td>{artifact.findingCount > 0 ? <span className="finding-count">{artifact.findingCount}</span> : '—'}</td>
                  <td className="hash-cell"><code title={artifact.sha256}>{artifact.sha256.slice(0, 12)}…</code></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="tree-panel" role="tree" aria-label="Artifact hierarchy">
          {tree.map((node) => <TreeNode key={node.id} node={node} depth={0} onSelectArtifact={onSelectArtifact} />)}
        </div>
      )}
    </section>
  );
}

function TreeNode({ node, depth, onSelectArtifact }: { node: ArtifactTreeNode; depth: number; onSelectArtifact: (id: string) => void }) {
  const [expanded, setExpanded] = useState(depth < 2);
  if (node.kind === 'artifact' && node.artifact) {
    return (
      <button type="button" role="treeitem" className="tree-row tree-artifact" style={{ '--tree-depth': depth } as CSSProperties} onClick={() => onSelectArtifact(node.artifact!.id)}>
        <span className="tree-toggle placeholder" aria-hidden="true" />
        <span className="tree-file-icon" aria-hidden="true">{node.artifact.extension ? node.artifact.extension.slice(1, 4).toUpperCase() : 'FILE'}</span>
        <span className="tree-copy"><strong>{node.name}</strong><small>{formatBytes(node.aggregateSize)} · {node.artifact.parseStatus.replace(/([a-z])([A-Z])/g, '$1 $2')}</small></span>
        {node.findingCount > 0 ? <span className="finding-count">{node.findingCount}</span> : null}
      </button>
    );
  }

  return (
    <div role="treeitem" aria-expanded={expanded} className="tree-folder">
      <button type="button" className="tree-row" style={{ '--tree-depth': depth } as CSSProperties} onClick={() => setExpanded((current) => !current)}>
        <span className="tree-toggle" aria-hidden="true">{expanded ? '⌄' : '›'}</span>
        <span className="tree-folder-icon" aria-hidden="true">□</span>
        <span className="tree-copy"><strong>{node.name}</strong><small>{node.children.length.toLocaleString()} items · {formatBytes(node.aggregateSize)}</small></span>
        {node.findingCount > 0 ? <span className="finding-count">{node.findingCount}</span> : null}
      </button>
      {expanded ? <div role="group">{node.children.map((child) => <TreeNode key={child.id} node={child} depth={depth + 1} onSelectArtifact={onSelectArtifact} />)}</div> : null}
    </div>
  );
}
