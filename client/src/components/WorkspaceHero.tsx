import type { CSSProperties, ChangeEvent } from 'react';
import { formatDate } from '../lib/format';
import type { Finding, ImportSummary, ProjectSummary, ViewId, WorkspaceInsights } from '../lib/types';

interface WorkspaceHeroProps {
  project: ProjectSummary;
  imports: ImportSummary[];
  selectedImport: ImportSummary | null;
  selectedImportId: string | null;
  insights: WorkspaceInsights;
  findings: Finding[];
  activeProfileName: string;
  onSelectImport: (importId: string) => void;
  onNavigate: (view: ViewId) => void;
  onCustomize: () => void;
  onOpenCommand: () => void;
}

export function WorkspaceHero({
  project,
  imports,
  selectedImport,
  selectedImportId,
  insights,
  findings,
  activeProfileName,
  onSelectImport,
  onNavigate,
  onCustomize,
  onOpenCommand,
}: WorkspaceHeroProps) {
  const errors = findings.filter((item) => item.severity === 'Error').length;
  const warnings = findings.filter((item) => item.severity === 'Warning').length;
  const decisionState = !selectedImport
    ? 'Awaiting evidence'
    : errors > 0
      ? 'Action required'
      : warnings > 0
        ? 'Review advised'
        : 'Evidence ready';
  const tone = errors > 0 ? 'danger' : warnings > 0 ? 'warning' : selectedImport ? 'success' : 'neutral';
  const primaryTarget: ViewId = errors + warnings > 0 ? 'findings' : 'review';
  const score = selectedImport ? insights.healthScore : 0;

  return (
    <section className={`workspace-command-hero tone-${tone}`} aria-labelledby="workspace-hero-title">
      <div className="hero-atmosphere" aria-hidden="true">
        <span className="hero-node node-a" />
        <span className="hero-node node-b" />
        <span className="hero-node node-c" />
        <span className="hero-path path-a" />
        <span className="hero-path path-b" />
      </div>

      <div className="hero-copy">
        <div className="hero-context-row">
          <span className="hero-kicker"><i /> Current decision posture</span>
          <span className={`hero-state state-${tone}`}>{decisionState}</span>
        </div>
        <h1 id="workspace-hero-title">{project.name}</h1>
        <p>
          {selectedImport
            ? `${selectedImport.displayName} is indexed as immutable evidence. Focus next on the highest-impact exceptions, then package a defensible handoff.`
            : 'Import a snapshot to establish evidence identity, parse coverage, findings, and review readiness.'}
        </p>

        <div className="hero-actions">
          <button type="button" className="primary-button hero-primary" onClick={() => onNavigate(primaryTarget)} disabled={!selectedImport}>
            <span aria-hidden="true">{errors + warnings > 0 ? '!' : '✓'}</span>
            {errors + warnings > 0 ? `Resolve ${errors + warnings} priority item${errors + warnings === 1 ? '' : 's'}` : 'Continue evidence review'}
          </button>
          <button type="button" className="secondary-button" onClick={() => onNavigate('compare')} disabled={imports.length < 2}>⇄ Inspect changes</button>
          <button type="button" className="secondary-button" onClick={onOpenCommand}>⌘ Ask the workspace</button>
        </div>

        <div className="hero-trust-row">
          <span>◉ Local processing</span>
          <span>◇ SHA-256 identity</span>
          <span>◐ Governed records excluded from preferences</span>
        </div>
      </div>

      <aside className="hero-control-deck" aria-label="Workspace readiness summary">
        <div className="hero-score-wrap">
          <div className="hero-score" style={{ '--hero-score': `${score}%` } as CSSProperties}>
            <div><strong>{selectedImport ? score : '—'}</strong><span>readiness</span></div>
          </div>
          <div className="hero-score-copy"><strong>{selectedImport ? insights.healthLabel : 'No snapshot'}</strong><span>Heuristic signal · not an approval gate</span></div>
        </div>

        <label className="hero-snapshot-picker">
          <span>Active snapshot</span>
          <select value={selectedImportId ?? ''} onChange={(event: ChangeEvent<HTMLSelectElement>) => onSelectImport(event.target.value)} disabled={imports.length === 0}>
            {imports.length === 0 ? <option value="">No snapshots</option> : imports.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}
          </select>
        </label>

        <div className="hero-mini-grid">
          <div><span>Coverage</span><strong>{selectedImport ? `${insights.parseCoveragePercent}%` : '—'}</strong><small>{insights.parsedCount.toLocaleString()} parsed</small></div>
          <div><span>Open risk</span><strong>{errors + warnings}</strong><small>{errors} errors · {warnings} warnings</small></div>
          <div><span>Profile</span><strong>{activeProfileName}</strong><button type="button" onClick={onCustomize}>Tune workspace</button></div>
          <div><span>Updated</span><strong>{formatDate(project.updatedAtUtc)}</strong><small>{imports.length} immutable snapshot{imports.length === 1 ? '' : 's'}</small></div>
        </div>
      </aside>
    </section>
  );
}
