import { useEffect, useMemo, useRef, useState, type ChangeEvent, type KeyboardEvent, type MouseEvent as ReactMouseEvent } from 'react';
import { api } from '../lib/api';
import type { SearchResult, ViewId } from '../lib/types';
import { StatusBadge } from './StatusBadge';

interface CommandPaletteProps {
  open: boolean;
  projectId: string | null;
  importId: string | null;
  onClose: () => void;
  onNavigate: (view: ViewId) => void;
  onInspectArtifact: (artifactId: string) => void;
  onCreateProject: () => void;
  onRenameProject: () => void;
}

const navigation: Array<{ view: ViewId; label: string; description: string; key: string }> = [
  { view: 'overview', label: 'Open overview', description: 'Import, metrics, and snapshot history', key: 'G O' },
  { view: 'inventory', label: 'Open inventory', description: 'Table and hierarchy explorer', key: 'G I' },
  { view: 'findings', label: 'Open findings', description: 'Validation evidence and recommendations', key: 'G F' },
  { view: 'compare', label: 'Compare snapshots', description: 'Added, removed, and modified artifacts', key: 'G C' },
  { view: 'operations', label: 'Open operations', description: 'Watches, profiles, impact, playbooks, and privacy', key: 'G P' },
  { view: 'decisions', label: 'Open decision center', description: 'Triage, baselines, automation, evidence answers, and handoff', key: 'G D' },
  { view: 'exports', label: 'Open exports', description: 'Generate HTML, CSV, and JSON reports', key: 'G E' },
];

export function CommandPalette({
  open,
  projectId,
  importId,
  onClose,
  onNavigate,
  onInspectArtifact,
  onCreateProject,
  onRenameProject,
}: CommandPaletteProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SearchResult>({ artifacts: [], findings: [] });
  const [loading, setLoading] = useState(false);
  const normalized = query.trim().toLowerCase();

  useEffect(() => {
    if (!open) {
      setQuery('');
      setResults({ artifacts: [], findings: [] });
      return;
    }
    window.setTimeout(() => inputRef.current?.focus(), 0);
  }, [open]);

  useEffect(() => {
    if (!open || !projectId || normalized.length < 2) {
      setResults({ artifacts: [], findings: [] });
      setLoading(false);
      return;
    }

    let cancelled = false;
    const timer = window.setTimeout(() => {
      setLoading(true);
      void api.search(projectId, normalized, importId ?? undefined)
        .then((value) => {
          if (!cancelled) setResults(value);
        })
        .catch(() => {
          if (!cancelled) setResults({ artifacts: [], findings: [] });
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }, 180);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [importId, normalized, open, projectId]);

  const visibleNavigation = useMemo(() => {
    if (!normalized) return navigation;
    return navigation.filter((item) => `${item.label} ${item.description}`.toLowerCase().includes(normalized));
  }, [normalized]);

  function navigate(view: ViewId) {
    onNavigate(view);
    onClose();
  }

  function handleInputKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Escape') onClose();
    if (event.key === 'Enter') {
      const artifact = results.artifacts[0];
      if (artifact) {
        onInspectArtifact(artifact.id);
        onClose();
      } else if (visibleNavigation[0]) {
        navigate(visibleNavigation[0].view);
      }
    }
  }

  if (!open) return null;

  return (
    <div className="command-backdrop" role="presentation" onMouseDown={(event: ReactMouseEvent<HTMLDivElement>) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className="command-palette" role="dialog" aria-modal="true" aria-label="Workbench command palette">
        <label className="command-input">
          <span aria-hidden="true">⌕</span>
          <span className="sr-only">Search commands, artifacts, and findings</span>
          <input ref={inputRef} value={query} onChange={(event: ChangeEvent<HTMLInputElement>) => setQuery(event.target.value)} onKeyDown={handleInputKeyDown} placeholder="Search commands, artifacts, findings…" />
          <kbd>Esc</kbd>
        </label>
        <div className="command-results">
          {visibleNavigation.length > 0 ? (
            <section className="command-group">
              <h2>Navigate</h2>
              {visibleNavigation.map((item) => (
                <button key={item.view} type="button" className="command-row" onClick={() => navigate(item.view)}>
                  <span className="command-symbol" aria-hidden="true">→</span>
                  <span><strong>{item.label}</strong><small>{item.description}</small></span>
                  <kbd>{item.key}</kbd>
                </button>
              ))}
            </section>
          ) : null}

          {!normalized ? (
            <section className="command-group">
              <h2>Workspace</h2>
              <button type="button" className="command-row" onClick={() => { onCreateProject(); onClose(); }}><span className="command-symbol" aria-hidden="true">+</span><span><strong>Create project</strong><small>Start a separate local workspace</small></span></button>
              {projectId ? <button type="button" className="command-row" onClick={() => { onRenameProject(); onClose(); }}><span className="command-symbol" aria-hidden="true">✎</span><span><strong>Rename project</strong><small>Update the current workspace label</small></span></button> : null}
            </section>
          ) : null}

          {results.artifacts.length > 0 ? (
            <section className="command-group">
              <h2>Artifacts</h2>
              {results.artifacts.slice(0, 8).map((artifact) => (
                <button key={artifact.id} type="button" className="command-row" onClick={() => { onInspectArtifact(artifact.id); onClose(); }}>
                  <span className="command-symbol file-command" aria-hidden="true">{artifact.extension ? artifact.extension.slice(1, 4).toUpperCase() : 'FILE'}</span>
                  <span><strong>{artifact.name}</strong><small>{artifact.relativePath}</small></span>
                  <StatusBadge value={artifact.parseStatus} compact />
                </button>
              ))}
            </section>
          ) : null}

          {results.findings.length > 0 ? (
            <section className="command-group">
              <h2>Findings</h2>
              {results.findings.slice(0, 8).map((finding) => (
                <button key={finding.id} type="button" className="command-row" onClick={() => { if (finding.artifactId) onInspectArtifact(finding.artifactId); else onNavigate('findings'); onClose(); }}>
                  <StatusBadge value={finding.severity} compact />
                  <span><strong>{finding.title}</strong><small>{finding.ruleId} · {finding.artifactPath ?? 'Project-level'}</small></span>
                </button>
              ))}
            </section>
          ) : null}

          {normalized.length >= 2 && !loading && results.artifacts.length === 0 && results.findings.length === 0 && visibleNavigation.length === 0 ? (
            <div className="command-empty"><strong>No matching workspace evidence</strong><span>Try a path segment, parser name, hash, rule ID, or finding text.</span></div>
          ) : null}
          {loading ? <div className="command-loading">Searching local metadata…</div> : null}
        </div>
        <footer className="command-footer"><span><kbd>Enter</kbd> open first result</span><span>Local search · current snapshot</span></footer>
      </section>
    </div>
  );
}
