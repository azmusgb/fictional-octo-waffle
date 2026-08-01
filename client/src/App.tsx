import { useCallback, useEffect, useMemo, useState, type ChangeEvent, type KeyboardEvent as ReactKeyboardEvent, type MouseEvent as ReactMouseEvent } from 'react';
import { ArtifactInspector } from './components/ArtifactInspector';
import { CommandPalette } from './components/CommandPalette';
import { CompareView } from './components/CompareView';
import { ImportDropzone } from './components/ImportDropzone';
import { InventoryExplorer } from './components/InventoryExplorer';
import { MetricCard } from './components/MetricCard';
import { ReviewQueue } from './components/ReviewQueue';
import { StatusBadge } from './components/StatusBadge';
import { SystemCenter } from './components/SystemCenter';
import { WorkspaceInsights } from './components/WorkspaceInsights';
import { api } from './lib/api';
import { formatBytes, formatDate, isActiveImport, progressPercent, severityRank } from './lib/format';
import { calculateWorkspaceInsights } from './lib/insights';
import type { AgentStatus, ArtifactListItem, Finding, ImportSummary, ProjectSummary, ViewId } from './lib/types';

const navItems: Array<{ id: ViewId; label: string; symbol: string; description: string }> = [
  { id: 'overview', label: 'Overview', symbol: '⌂', description: 'Health, import, and snapshot intelligence' },
  { id: 'inventory', label: 'Explorer', symbol: '▤', description: 'Files, hierarchy, parsers, and evidence' },
  { id: 'review', label: 'Review queue', symbol: '✓', description: 'Human decisions, notes, tags, and follow-up' },
  { id: 'findings', label: 'Findings', symbol: '!', description: 'Validation, source evidence, and actions' },
  { id: 'compare', label: 'Compare', symbol: '⇄', description: 'Immutable snapshot differences' },
  { id: 'exports', label: 'Exports', symbol: '⇩', description: 'Portable reports and machine-readable data' },
  { id: 'system', label: 'System center', symbol: '⚙', description: 'Local agent connection, limits, and diagnostics' },
];

const pageDescriptions: Record<ViewId, string> = {
  overview: 'Monitor snapshot readiness, import new evidence, and understand the structure and risk profile of the current workspace.',
  inventory: 'Filter or traverse the artifact hierarchy, inspect parser output, and trace every finding back to source content.',
  review: 'Move artifacts through an explicit review lifecycle, capture local notes, and classify follow-up with bounded tags.',
  findings: 'Prioritize validation issues, source locations, evidence excerpts, and concrete recommended actions.',
  compare: 'Use normalized paths and SHA-256 identity to compare versions without altering either snapshot.',
  exports: 'Generate portable inventory, evidence reports, and a project-level manifest from immutable local records.',
  system: 'Verify the local processing agent, inspect its safety envelope, and configure a hosted or same-origin browser shell.',
};

function App() {
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(() => localStorage.getItem('ws.project'));
  const [imports, setImports] = useState<ImportSummary[]>([]);
  const [selectedImportId, setSelectedImportId] = useState<string | null>(null);
  const [artifacts, setArtifacts] = useState<ArtifactListItem[]>([]);
  const [findings, setFindings] = useState<Finding[]>([]);
  const [selectedArtifactId, setSelectedArtifactId] = useState<string | null>(null);
  const [view, setView] = useState<ViewId>('overview');
  const [findingFilter, setFindingFilter] = useState<'All' | 'Error' | 'Warning' | 'Info'>('All');
  const [findingSearch, setFindingSearch] = useState('');
  const [loadingProjects, setLoadingProjects] = useState(true);
  const [loadingWorkspace, setLoadingWorkspace] = useState(false);
  const [importBusy, setImportBusy] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [createName, setCreateName] = useState('');
  const [createBusy, setCreateBusy] = useState(false);
  const [renameOpen, setRenameOpen] = useState(false);
  const [renameName, setRenameName] = useState('');
  const [renameBusy, setRenameBusy] = useState(false);
  const [commandOpen, setCommandOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [agentStatus, setAgentStatus] = useState<AgentStatus | null>(null);
  const [agentUnavailable, setAgentUnavailable] = useState(false);
  const [theme, setTheme] = useState<'light' | 'dark'>(() =>
    (localStorage.getItem('ws.theme') as 'light' | 'dark' | null) ??
    (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'),
  );
  const [density, setDensity] = useState<'comfortable' | 'compact'>(() =>
    (localStorage.getItem('ws.density') as 'comfortable' | 'compact' | null) ?? 'comfortable',
  );

  const selectedProject = projects.find((project) => project.id === selectedProjectId) ?? null;
  const selectedImport = imports.find((item) => item.id === selectedImportId) ?? null;
  const insights = useMemo(() => calculateWorkspaceInsights(artifacts, findings), [artifacts, findings]);

  const refreshProjects = useCallback(async () => {
    const result = await api.getProjects();
    setProjects(result);
    setSelectedProjectId((current) => {
      if (current && result.some((project) => project.id === current)) return current;
      return result[0]?.id ?? null;
    });
    return result;
  }, []);

  const refreshImports = useCallback(async (projectId: string, preserveSelection = true) => {
    const result = await api.getImports(projectId);
    setImports(result);
    setSelectedImportId((current) => {
      if (preserveSelection && current && result.some((item) => item.id === current)) return current;
      const preferred = result.find((item) => ['Completed', 'CompletedWithWarnings'].includes(item.status)) ?? result[0];
      return preferred?.id ?? null;
    });
    return result;
  }, []);

  const refreshSnapshotData = useCallback(async (projectId: string, importId: string) => {
    const [artifactPage, findingRows] = await Promise.all([
      api.getArtifacts(importId),
      api.getFindings(projectId, importId),
    ]);
    setArtifacts(artifactPage.items);
    setFindings(findingRows);
  }, []);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem('ws.theme', theme);
  }, [theme]);

  useEffect(() => {
    document.documentElement.dataset.density = density;
    localStorage.setItem('ws.density', density);
  }, [density]);

  useEffect(() => {
    let cancelled = false;
    setLoadingProjects(true);
    setError(null);
    void api.getAgentStatus()
      .then((status) => {
        if (cancelled) return [] as ProjectSummary[];
        setAgentStatus(status);
        setAgentUnavailable(false);
        return refreshProjects();
      })
      .catch((reason: unknown) => {
        if (!cancelled) {
          setAgentStatus(null);
          setAgentUnavailable(true);
          setError(reason instanceof Error ? reason.message : 'Could not connect to the local processing agent.');
        }
        return [] as ProjectSummary[];
      })
      .finally(() => {
        if (!cancelled) setLoadingProjects(false);
      });
    return () => { cancelled = true; };
  }, [refreshProjects]);

  useEffect(() => {
    if (!selectedProjectId) {
      setImports([]);
      setSelectedImportId(null);
      return;
    }
    localStorage.setItem('ws.project', selectedProjectId);
    let cancelled = false;
    setLoadingWorkspace(true);
    setError(null);
    void refreshImports(selectedProjectId, false)
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'Could not load project imports.');
      })
      .finally(() => {
        if (!cancelled) setLoadingWorkspace(false);
      });
    return () => { cancelled = true; };
  }, [refreshImports, selectedProjectId]);

  useEffect(() => {
    if (!selectedProjectId || !selectedImportId) {
      setArtifacts([]);
      setFindings([]);
      return;
    }
    let cancelled = false;
    setLoadingWorkspace(true);
    void refreshSnapshotData(selectedProjectId, selectedImportId)
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'Could not load snapshot data.');
      })
      .finally(() => {
        if (!cancelled) setLoadingWorkspace(false);
      });
    return () => { cancelled = true; };
  }, [refreshSnapshotData, selectedImportId, selectedProjectId]);

  useEffect(() => {
    if (!selectedProjectId || !imports.some((item) => isActiveImport(item.status))) return;
    const timer = window.setInterval(() => {
      void refreshImports(selectedProjectId).then((rows) => {
        const selected = rows.find((item) => item.id === selectedImportId);
        if (selected && !isActiveImport(selected.status) && selectedImportId) {
          void refreshSnapshotData(selectedProjectId, selectedImportId);
          void refreshProjects();
        }
      }).catch(() => {
        // Keep the last usable workspace during a transient polling failure.
      });
    }, 1500);
    return () => window.clearInterval(timer);
  }, [imports, refreshImports, refreshProjects, refreshSnapshotData, selectedImportId, selectedProjectId]);

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      const isTyping = target?.matches('input, textarea, select, [contenteditable="true"]') ?? false;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        setCommandOpen((current) => !current);
        return;
      }
      if (event.key === 'Escape') {
        setSelectedArtifactId(null);
        setCreateOpen(false);
        setRenameOpen(false);
        setCommandOpen(false);
      }
      if (!isTyping && event.key === '/') {
        event.preventDefault();
        setCommandOpen(true);
      }
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 3500);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const filteredFindings = useMemo(() => {
    const term = findingSearch.trim().toLowerCase();
    const rows = findings.filter((item) => {
      if (findingFilter !== 'All' && item.severity !== findingFilter) return false;
      if (!term) return true;
      return [item.title, item.message, item.ruleId, item.artifactPath ?? '', item.sourceLocation ?? '']
        .some((value) => value.toLowerCase().includes(term));
    });
    return [...rows].sort((left, right) => severityRank(right.severity) - severityRank(left.severity));
  }, [findingFilter, findingSearch, findings]);

  const activeImports = imports.filter((item) => isActiveImport(item.status));

  async function createProject() {
    const name = createName.trim();
    if (!name) return;
    setCreateBusy(true);
    setError(null);
    try {
      const created = await api.createProject(name);
      await refreshProjects();
      setSelectedProjectId(created.id);
      setCreateName('');
      setCreateOpen(false);
      setToast('Project created.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not create the project.');
    } finally {
      setCreateBusy(false);
    }
  }

  async function renameProject() {
    if (!selectedProjectId || !renameName.trim()) return;
    setRenameBusy(true);
    setError(null);
    try {
      await api.renameProject(selectedProjectId, renameName.trim());
      await refreshProjects();
      setRenameOpen(false);
      setToast('Project renamed.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not rename the project.');
    } finally {
      setRenameBusy(false);
    }
  }

  async function createImport(files: File[], displayName: string) {
    if (!selectedProjectId) return;
    setImportBusy(true);
    setError(null);
    try {
      const created = await api.createImport(selectedProjectId, files, displayName);
      await refreshImports(selectedProjectId, false);
      setSelectedImportId(created.id);
      setToast('Snapshot queued for local processing.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not upload the selected files.');
      throw reason;
    } finally {
      setImportBusy(false);
    }
  }

  async function cancelImport(importId: string) {
    try {
      await api.cancelImport(importId);
      if (selectedProjectId) await refreshImports(selectedProjectId);
      setToast('Cancellation requested.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not cancel the import.');
    }
  }

  async function retryImport(importId: string) {
    try {
      await api.retryImport(importId);
      if (selectedProjectId) await refreshImports(selectedProjectId);
      setSelectedImportId(importId);
      setToast('Import retry queued.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not retry the import.');
    }
  }

  function selectProject(projectId: string) {
    setSelectedProjectId(projectId);
    setSelectedArtifactId(null);
    setView('overview');
  }

  function navigate(nextView: ViewId) {
    setView(nextView);
    setSelectedArtifactId(null);
  }

  function openRenameDialog() {
    if (!selectedProject) return;
    setRenameName(selectedProject.name);
    setRenameOpen(true);
  }

  if (loadingProjects) {
    return (
      <main className="boot-screen">
        <div className="brand-mark large-mark" aria-hidden="true">W</div>
        <h1>Workbench Studio</h1>
        <p>Opening the local evidence workspace…</p>
      </main>
    );
  }

  return (
    <div className={`app-shell${selectedArtifactId ? ' inspector-open' : ''}`}>
      <header className="topbar">
        <div className="brand-lockup">
          <div className="brand-mark" aria-hidden="true">W</div>
          <div><strong>Workbench Studio</strong><span>Evidence operations · v4</span></div>
        </div>

        <div className="topbar-center">
          <label className="project-select-label">
            <span className="sr-only">Current project</span>
            <select value={selectedProjectId ?? ''} onChange={(event: ChangeEvent<HTMLSelectElement>) => selectProject(event.target.value)} disabled={projects.length === 0}>
              {projects.length === 0 ? <option value="">No projects</option> : null}
              {projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
            </select>
          </label>
          <button type="button" className="command-trigger" onClick={() => setCommandOpen(true)} aria-label="Open command palette">
            <span aria-hidden="true">⌕</span><span>Search workspace</span><kbd>{navigator.platform.includes('Mac') ? '⌘' : 'Ctrl'} K</kbd>
          </button>
        </div>

        <div className="topbar-actions">
          <button type="button" className="secondary-button compact-button" onClick={() => setCreateOpen(true)}>+ New project</button>
          {selectedProject ? <button type="button" className="icon-button" aria-label="Rename current project" title="Rename project" onClick={openRenameDialog}>✎</button> : null}
          <button type="button" className="icon-button" aria-label={`Use ${density === 'comfortable' ? 'compact' : 'comfortable'} density`} title="Toggle density" onClick={() => setDensity((current) => current === 'comfortable' ? 'compact' : 'comfortable')}>{density === 'comfortable' ? '≡' : '☰'}</button>
          <button type="button" className="icon-button" aria-label={`Use ${theme === 'light' ? 'dark' : 'light'} theme`} title="Toggle theme" onClick={() => setTheme((current) => current === 'light' ? 'dark' : 'light')}>{theme === 'light' ? '◐' : '☀'}</button>
        </div>
      </header>

      <aside className="sidebar" aria-label="Primary navigation">
        <nav>
          {navItems.map((item) => (
            <button key={item.id} type="button" className={view === item.id ? 'nav-item is-active' : 'nav-item'} onClick={() => navigate(item.id)} disabled={!selectedProject && item.id !== 'overview' && item.id !== 'system'} aria-current={view === item.id ? 'page' : undefined}>
              <span className="nav-symbol" aria-hidden="true">{item.symbol}</span>
              <span><strong>{item.label}</strong><small>{item.description}</small></span>
              {item.id === 'findings' && findings.length > 0 ? <span className="nav-count">{findings.length}</span> : null}
              {item.id === 'review' && artifacts.filter((artifact) => artifact.reviewStatus !== 'Accepted').length > 0 ? <span className="nav-count">{artifacts.filter((artifact) => artifact.reviewStatus !== 'Accepted').length}</span> : null}
            </button>
          ))}
        </nav>
        <div className="shortcut-guide">
          <span>Keyboard</span>
          <div><kbd>Ctrl K</kbd><small>Command palette</small></div>
          <div><kbd>/</kbd><small>Search</small></div>
          <div><kbd>Esc</kbd><small>Close panels</small></div>
        </div>
        <div className="sidebar-footer">
          <span className={`local-indicator${agentUnavailable ? ' is-offline' : ''}`}><span aria-hidden="true" /> {agentUnavailable ? 'Agent unavailable' : 'Local agent connected'}</span>
          <small>{agentUnavailable ? 'Open System Center to configure the connection.' : 'Original artifacts remain on this device.'}</small>
        </div>
      </aside>

      <main className="main-workspace" id="main-content">
        {error ? <div className="global-error" role="alert"><div><strong>Workbench action failed</strong><span>{error}</span></div><button type="button" className="icon-button" aria-label="Dismiss error" onClick={() => setError(null)}>×</button></div> : null}

        {view === 'system' ? (
          <>
            <section className="page-header">
              <div><span className="eyebrow">{navItems.find((item) => item.id === view)?.description}</span><h1>System Center</h1><p>{pageDescriptions.system}</p></div>
            </section>
            <SystemCenter initialStatus={agentStatus} />
          </>
        ) : !selectedProject ? (
          <section className="welcome-state">
            <div className="welcome-visual" aria-hidden="true"><span>JSON</span><span>CSV</span><span>XML</span><span>XLSX</span></div>
            <span className="eyebrow">{agentUnavailable ? 'Browser shell ready · local agent required' : 'Local-first engineering workbench'}</span>
            <h1>{agentUnavailable ? 'Connect the processing agent to open your evidence workspace.' : 'Turn project artifacts into an inspectable evidence model.'}</h1>
            <p>{agentUnavailable ? 'The hosted or local React shell contains no project data. Configure the ASP.NET Core agent URL in System Center; parsing, SQLite, originals, reviews, and evidence remain on your device.' : 'Create a project, import a ZIP or individual files, and Workbench Studio will inventory, hash, parse, validate, compare, review, and report on the contents without a cloud dependency.'}</p>
            <div className="welcome-actions">{agentUnavailable ? <button type="button" className="primary-button large-button" onClick={() => setView('system')}>Open System Center</button> : <button type="button" className="primary-button large-button" onClick={() => setCreateOpen(true)}>Create first project</button>}<button type="button" className="secondary-button large-button" onClick={() => setCommandOpen(true)}>Explore commands</button></div>
          </section>
        ) : (
          <>
            <section className="page-header">
              <div>
                <span className="eyebrow">{navItems.find((item) => item.id === view)?.description}</span>
                <h1>{view === 'overview' ? selectedProject.name : navItems.find((item) => item.id === view)?.label}</h1>
                <p>{view === 'overview' ? `${imports.length.toLocaleString()} immutable snapshot${imports.length === 1 ? '' : 's'} · updated ${formatDate(selectedProject.updatedAtUtc)}` : pageDescriptions[view]}</p>
              </div>
              {imports.length > 0 && view !== 'compare' ? (
                <label className="snapshot-select field-label">Snapshot<select value={selectedImportId ?? ''} onChange={(event: ChangeEvent<HTMLSelectElement>) => setSelectedImportId(event.target.value)}>{imports.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}</select></label>
              ) : null}
            </section>

            {loadingWorkspace && imports.length === 0 ? <div className="panel-loading">Loading project workspace…</div> : null}

            {view === 'overview' ? (
              <div className="page-stack">
                <section className="metrics-grid">
                  <MetricCard label="Health score" value={selectedImport ? insights.healthScore : '—'} detail={selectedImport ? insights.healthLabel : 'Import a snapshot'} tone={insights.healthScore >= 75 ? 'success' : insights.healthScore >= 55 ? 'warning' : selectedImport ? 'danger' : 'default'} />
                  <MetricCard label="Parse coverage" value={selectedImport ? `${insights.parseCoveragePercent}%` : '—'} detail={`${insights.parsedCount.toLocaleString()} parsed · ${insights.unsupportedCount.toLocaleString()} unsupported`} tone="success" />
                  <MetricCard label="Findings" value={findings.length.toLocaleString()} detail={`${findings.filter((item) => item.severity === 'Error').length} errors · ${findings.filter((item) => item.severity === 'Warning').length} warnings`} tone={findings.some((item) => item.severity === 'Error') ? 'danger' : findings.length > 0 ? 'warning' : 'default'} />
                  <MetricCard label="Indexed evidence" value={formatBytes(insights.totalBytes)} detail={`${artifacts.length.toLocaleString()} SHA-256 verified artifacts`} />
                </section>

                {activeImports.map((item) => (
                  <section key={item.id} className="processing-card" aria-live="polite">
                    <div className="processing-header"><div><StatusBadge value={item.status} /><strong>{item.displayName}</strong><span>{item.statusMessage}</span></div><button type="button" className="danger-text-button" onClick={() => void cancelImport(item.id)} disabled={item.cancellationRequested}>{item.cancellationRequested ? 'Cancelling…' : 'Cancel'}</button></div>
                    <div className="progress-track" role="progressbar" aria-label={`${item.displayName} progress`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={progressPercent(item.processedFiles, item.totalFiles)}><span style={{ width: `${progressPercent(item.processedFiles, item.totalFiles)}%` }} /></div>
                    <div className="processing-meta"><span>{item.currentStage}</span><span>{item.processedFiles.toLocaleString()} / {item.totalFiles.toLocaleString()} artifacts</span></div>
                  </section>
                ))}

                <div className="overview-grid">
                  <ImportDropzone disabled={!selectedProjectId} busy={importBusy} onImport={createImport} />
                  <section className="surface-card snapshot-card">
                    <div className="section-heading compact-heading"><div><span className="eyebrow">History</span><h2>Import snapshots</h2></div><span className="quiet-label">Newest first</span></div>
                    {imports.length === 0 ? <div className="empty-panel">No snapshots yet. Import a ZIP or one or more supported files.</div> : (
                      <div className="snapshot-list">
                        {imports.map((item) => (
                          <div key={item.id} className={selectedImportId === item.id ? 'snapshot-row-wrap is-selected' : 'snapshot-row-wrap'}>
                            <button type="button" className="snapshot-row" onClick={() => { setSelectedImportId(item.id); setView('inventory'); }}>
                              <span className="snapshot-icon" aria-hidden="true">◇</span>
                              <span className="snapshot-copy"><strong>{item.displayName}</strong><small>{formatDate(item.createdAtUtc)} · {item.totalFiles.toLocaleString()} artifacts · {formatBytes(item.totalBytes)}</small></span>
                              <StatusBadge value={item.status} compact />
                            </button>
                            {['Failed', 'Cancelled'].includes(item.status) ? <button type="button" className="retry-button" onClick={() => void retryImport(item.id)}>Retry</button> : null}
                          </div>
                        ))}
                      </div>
                    )}
                  </section>
                </div>

                {selectedImport ? <WorkspaceInsights insights={insights} artifacts={artifacts} findings={findings} imports={imports} onNavigate={navigate} onInspectArtifact={setSelectedArtifactId} /> : null}
              </div>
            ) : null}

            {view === 'inventory' ? selectedImport ? <InventoryExplorer artifacts={artifacts} onSelectArtifact={setSelectedArtifactId} /> : <EmptySnapshot title="No snapshot selected" description="Import files or select a snapshot from the overview." /> : null}

            {view === 'review' ? selectedImport ? <ReviewQueue artifacts={artifacts} onInspectArtifact={setSelectedArtifactId} /> : <EmptySnapshot title="No snapshot selected" description="Select a completed import to triage artifact review state." /> : null}

            {view === 'findings' ? selectedImport ? (
              <div className="page-stack">
                <section className="finding-summary-grid">
                  {(['Error', 'Warning', 'Info'] as const).map((severity) => <button key={severity} type="button" className={`severity-summary severity-${severity.toLowerCase()}${findingFilter === severity ? ' is-active' : ''}`} onClick={() => setFindingFilter(findingFilter === severity ? 'All' : severity)}><span>{severity}</span><strong>{findings.filter((item) => item.severity === severity).length.toLocaleString()}</strong></button>)}
                  <button type="button" className={`severity-summary${findingFilter === 'All' ? ' is-active' : ''}`} onClick={() => setFindingFilter('All')}><span>All findings</span><strong>{findings.length.toLocaleString()}</strong></button>
                </section>
                <section className="surface-card findings-toolbar"><div><strong>{filteredFindings.length.toLocaleString()} visible findings</strong><span>Search title, rule, artifact path, source location, or message.</span></div><label className="search-field expanded-search"><span aria-hidden="true">⌕</span><span className="sr-only">Search findings</span><input value={findingSearch} onChange={(event: ChangeEvent<HTMLInputElement>) => setFindingSearch(event.target.value)} placeholder="Search validation evidence…" />{findingSearch ? <button type="button" className="clear-input" aria-label="Clear findings search" onClick={() => setFindingSearch('')}>×</button> : null}</label></section>
                <section className="findings-list" aria-live="polite">
                  {filteredFindings.length > 0 ? filteredFindings.map((finding) => (
                    <article key={finding.id} className="finding-card">
                      <div className="finding-title-row"><div><StatusBadge value={finding.severity} /><code>{finding.ruleId}</code></div>{finding.artifactId ? <button type="button" className="text-button" onClick={() => setSelectedArtifactId(finding.artifactId)}>Inspect artifact</button> : null}</div>
                      <h2>{finding.title}</h2><p>{finding.message}</p>
                      <dl className="finding-evidence-grid"><div><dt>Artifact</dt><dd>{finding.artifactPath ?? 'Project-level finding'}</dd></div><div><dt>Source location</dt><dd>{finding.sourceLocation ?? 'Not specified'}</dd></div></dl>
                      {finding.evidenceExcerpt ? <blockquote>{finding.evidenceExcerpt}</blockquote> : null}
                      {finding.recommendation ? <div className="recommendation"><strong>Recommended action</strong><span>{finding.recommendation}</span></div> : null}
                    </article>
                  )) : <div className="empty-state large-empty"><div className="empty-icon" aria-hidden="true">✓</div><h2>No findings in this view</h2><p>The selected snapshot has no findings matching the current severity and search filters.</p></div>}
                </section>
              </div>
            ) : <EmptySnapshot title="No snapshot selected" description="Select a completed import to review validation findings." /> : null}

            {view === 'compare' ? <CompareView projectId={selectedProject.id} imports={imports} /> : null}

            {view === 'exports' ? selectedImport ? (
              <div className="exports-grid">
                {([
                  { format: 'html' as const, title: 'HTML evidence report', description: 'A self-contained report with health context, findings, evidence, and the complete artifact inventory.', symbol: 'HTML' },
                  { format: 'csv' as const, title: 'CSV inventory', description: 'A spreadsheet-ready inventory containing paths, types, sizes, hashes, parser status, and parser identifiers.', symbol: 'CSV' },
                  { format: 'json' as const, title: 'JSON project export', description: 'A structured machine-readable package containing project metadata, snapshot state, artifacts, and findings.', symbol: 'JSON' },
                ]).map((item) => <article key={item.format} className="export-card"><div className="export-symbol" aria-hidden="true">{item.symbol}</div><div><span className="eyebrow">Portable export</span><h2>{item.title}</h2><p>{item.description}</p></div><a className="primary-button" href={api.exportUrl(selectedProject.id, selectedImport.id, item.format)} download>Generate and download</a></article>)}
                <article className="export-card manifest-export-card"><div className="export-symbol" aria-hidden="true">MAN</div><div><span className="eyebrow">Project portability</span><h2>Project evidence manifest</h2><p>Download a project-level manifest containing snapshot metadata, hashes, parser identity, findings, review decisions, notes, and tags without copying original file bytes.</p></div><a className="primary-button" href={api.projectManifestUrl(selectedProject.id)} download>Download manifest</a></article>
                <section className="surface-card export-context"><span className="eyebrow">Export context</span><h2>{selectedImport.displayName}</h2><dl className="property-list"><div><dt>Status</dt><dd><StatusBadge value={selectedImport.status} compact /></dd></div><div><dt>Artifacts</dt><dd>{selectedImport.totalFiles.toLocaleString()}</dd></div><div><dt>Parse coverage</dt><dd>{insights.parseCoveragePercent}%</dd></div><div><dt>Warnings</dt><dd>{selectedImport.warningCount.toLocaleString()}</dd></div><div><dt>Errors</dt><dd>{selectedImport.errorCount.toLocaleString()}</dd></div><div><dt>Imported</dt><dd>{formatDate(selectedImport.createdAtUtc)}</dd></div></dl></section>
              </div>
            ) : <EmptySnapshot title="No snapshot selected" description="Select a completed import before generating reports." /> : null}
          </>
        )}
      </main>

      <ArtifactInspector artifactId={selectedArtifactId} onClose={() => setSelectedArtifactId(null)} onToast={setToast} onReviewUpdated={(artifactId, review) => { setArtifacts((rows) => rows.map((artifact) => artifact.id === artifactId ? { ...artifact, reviewStatus: review.status, reviewUpdatedAtUtc: review.updatedAtUtc } : artifact)); }} />

      <footer className="statusbar">
        <span><span className={`statusbar-dot${agentUnavailable ? ' is-offline' : ''}`} aria-hidden="true" /> {agentUnavailable ? 'Agent disconnected' : `Agent ${agentStatus?.version ?? 'connected'}`}</span>
        <span>{selectedProject?.name ?? 'No project selected'}</span>
        <span>{selectedImport ? `${selectedImport.totalFiles.toLocaleString()} artifacts · ${insights.parseCoveragePercent}% parsed · ${selectedImport.warningCount.toLocaleString()} warnings · ${selectedImport.errorCount.toLocaleString()} errors` : 'No active snapshot'}</span>
      </footer>

      <CommandPalette open={commandOpen} projectId={selectedProjectId} importId={selectedImportId} onClose={() => setCommandOpen(false)} onNavigate={navigate} onInspectArtifact={setSelectedArtifactId} onCreateProject={() => setCreateOpen(true)} onRenameProject={openRenameDialog} />

      {createOpen ? <ProjectDialog mode="create" name={createName} busy={createBusy} onNameChange={setCreateName} onClose={() => setCreateOpen(false)} onSubmit={() => void createProject()} /> : null}
      {renameOpen ? <ProjectDialog mode="rename" name={renameName} busy={renameBusy} onNameChange={setRenameName} onClose={() => setRenameOpen(false)} onSubmit={() => void renameProject()} /> : null}

      {toast ? <div className="toast" role="status">{toast}</div> : null}
    </div>
  );
}

function EmptySnapshot({ title, description }: { title: string; description: string }) {
  return <div className="empty-state large-empty"><h2>{title}</h2><p>{description}</p></div>;
}

function ProjectDialog({ mode, name, busy, onNameChange, onClose, onSubmit }: {
  mode: 'create' | 'rename';
  name: string;
  busy: boolean;
  onNameChange: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
}) {
  const title = mode === 'create' ? 'Create a project' : 'Rename project';
  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={(event: ReactMouseEvent<HTMLDivElement>) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className="modal-card" role="dialog" aria-modal="true" aria-labelledby="project-dialog-heading">
        <div className="modal-header"><div><span className="eyebrow">{mode === 'create' ? 'New workspace' : 'Workspace identity'}</span><h2 id="project-dialog-heading">{title}</h2></div><button type="button" className="icon-button" aria-label="Close dialog" onClick={onClose}>×</button></div>
        <p>{mode === 'create' ? 'A project groups immutable imports, findings, comparisons, and generated reports in one local directory.' : 'Renaming changes the display label only. Snapshot identity, hashes, and stored evidence remain unchanged.'}</p>
        <label className="field-label">Project name<input autoFocus value={name} onChange={(event: ChangeEvent<HTMLInputElement>) => onNameChange(event.target.value)} onKeyDown={(event: ReactKeyboardEvent<HTMLInputElement>) => { if (event.key === 'Enter') onSubmit(); }} maxLength={200} placeholder="Example: FormWorks production review" /></label>
        <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose} disabled={busy}>Cancel</button><button type="button" className="primary-button" onClick={onSubmit} disabled={busy || !name.trim()}>{busy ? 'Saving…' : mode === 'create' ? 'Create project' : 'Save name'}</button></div>
      </section>
    </div>
  );
}

export default App;
