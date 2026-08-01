import { useCallback, useEffect, useMemo, useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react';
import { api } from '../lib/api';
import { formatDate } from '../lib/format';
import type { DataProfile, LineageEdge, Playbook, PrivacyDetection, WatchFolder } from '../lib/types';

interface OperationsCenterProps {
  projectId: string;
  importId: string | null;
  onToast: (message: string) => void;
}

type OperationsTab = 'setup' | 'watch' | 'profile' | 'impact' | 'playbooks' | 'privacy';

const starterSteps = [
  { id: 'profile', name: 'Profile current snapshot', type: 'profile', required: true, configuration: null },
  { id: 'privacy', name: 'Scan sensitive values', type: 'privacy', required: true, configuration: null },
  { id: 'impact', name: 'Build lineage and impact', type: 'impact', required: true, configuration: null },
  { id: 'review', name: 'Prepare review queue', type: 'review', required: true, configuration: null },
];

export function OperationsCenter({ projectId, importId, onToast }: OperationsCenterProps) {
  const [tab, setTab] = useState<OperationsTab>('setup');
  const [watches, setWatches] = useState<WatchFolder[]>([]);
  const [profiles, setProfiles] = useState<DataProfile[]>([]);
  const [lineage, setLineage] = useState<LineageEdge[]>([]);
  const [playbooks, setPlaybooks] = useState<Playbook[]>([]);
  const [privacy, setPrivacy] = useState<PrivacyDetection[]>([]);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [watchName, setWatchName] = useState('Nightly evidence');
  const [watchPath, setWatchPath] = useState('C:\\Evidence\\Nightly');
  const [setupStep, setSetupStep] = useState(0);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [watchRows, playbookRows] = await Promise.all([api.getWatchFolders(projectId), api.getPlaybooks(projectId)]);
      setWatches(watchRows);
      setPlaybooks(playbookRows);
      if (importId) {
        const [profileRows, lineageRows, privacyRows] = await Promise.all([
          api.getProfiles(projectId, importId), api.getLineage(projectId, importId), api.getPrivacyDetections(projectId, importId),
        ]);
        setProfiles(profileRows);
        setLineage(lineageRows);
        setPrivacy(privacyRows);
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not load operations data.');
    }
  }, [projectId, importId]);

  useEffect(() => { void load(); }, [load]);

  async function execute(key: string, action: () => Promise<unknown>, success: string) {
    setBusy(key); setError(null);
    try { await action(); onToast(success); await load(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Operation failed.'); }
    finally { setBusy(null); }
  }

  async function addWatch(event: FormEvent) {
    event.preventDefault();
    await execute('create-watch', () => api.createWatchFolder(projectId, {
      name: watchName, folderPath: watchPath, triggerMode: 'Hourly', scanIntervalMinutes: 60,
      ignorePatterns: ['*.tmp', '**/archive/**'], requireApproval: true,
    }), 'Watch folder created.');
  }

  const privacyGroups = useMemo(() => Object.entries(privacy.reduce<Record<string, number>>((result, item) => {
    result[item.kind] = (result[item.kind] ?? 0) + 1; return result;
  }, {})), [privacy]);
  const affectedArtifacts = useMemo(() => new Set(lineage.flatMap((edge) => [edge.fromArtifactId, edge.toArtifactId].filter(Boolean))).size, [lineage]);

  return (
    <div className="operations-center">
      <div className="operations-tabs" role="tablist">
        {([
          ['setup', 'Agent setup'], ['watch', 'Watch folders'], ['profile', 'Profiler'], ['impact', 'Impact'], ['playbooks', 'Playbooks'], ['privacy', 'Privacy'],
        ] as Array<[OperationsTab, string]>).map(([id, label]) => <button key={id} type="button" className={tab === id ? 'is-active' : ''} onClick={() => setTab(id)}>{label}</button>)}
      </div>
      {error ? <div className="global-error" role="alert">{error}</div> : null}

      {tab === 'setup' ? <AgentSetup step={setupStep} onBack={() => setSetupStep((current) => Math.max(0, current - 1))} onNext={() => setSetupStep((current) => Math.min(4, current + 1))} /> : null}

      {tab === 'watch' ? <div className="operations-grid">
        <section className="surface-card operations-panel">
          <span className="eyebrow">Continuous evidence intake</span><h2>Watch folders</h2><p>Create immutable snapshots when local evidence changes. Automatic schedules run no more frequently than hourly.</p>
          <form className="operations-form" onSubmit={(event: FormEvent) => void addWatch(event)}><label>Name<input value={watchName} onChange={(event: ChangeEvent<HTMLInputElement>) => setWatchName(event.target.value)} /></label><label>Local folder path<input value={watchPath} onChange={(event: ChangeEvent<HTMLInputElement>) => setWatchPath(event.target.value)} /></label><button className="primary-button" disabled={busy === 'create-watch'}>{busy === 'create-watch' ? 'Creating…' : 'Add watch folder'}</button></form>
        </section>
        <section className="surface-card operations-panel wide-panel"><div className="section-heading"><div><span className="eyebrow">Configured watches</span><h2>{watches.length.toLocaleString()} folders</h2></div><button className="secondary-button" onClick={() => void load()}>Refresh</button></div>
          <div className="operations-list">{watches.map((watch) => <article key={watch.id}><div><strong>{watch.name}</strong><span>{watch.folderPath}</span><small>{watch.triggerMode} · Last scan {watch.lastScannedAtUtc ? formatDate(watch.lastScannedAtUtc) : 'never'}</small></div><div className="operation-actions"><button className="secondary-button" onClick={() => void execute(`toggle-${watch.id}`, () => api.updateWatchFolder(projectId, watch.id, { enabled: !watch.enabled }), `${watch.name} ${watch.enabled ? 'paused' : 'enabled'}.`)}>{watch.enabled ? 'Pause' : 'Enable'}</button><button className="primary-button" onClick={() => void execute(`scan-${watch.id}`, () => api.scanWatchFolder(projectId, watch.id), `${watch.name} scan queued.`)} disabled={busy === `scan-${watch.id}`}>Scan now</button></div></article>)}</div>
        </section>
      </div> : null}

      {tab === 'profile' ? <SnapshotOperation title="Data Quality Profiler" description="Measure completeness, duplicate content, schema drift, parser failures, and format-specific structure before record-level review." disabled={!importId} actionLabel="Profile snapshot" busy={busy === 'profile'} onAction={() => importId && void execute('profile', () => api.runProfiles(projectId, importId), 'Data profile completed.')}>
        <div className="operations-metrics"><Metric label="Profiles" value={profiles.length} /><Metric label="Issues" value={profiles.reduce((count, profile) => count + profile.issues.length, 0)} /><Metric label="Duplicate signals" value={profiles.filter((profile) => Number(profile.metrics.duplicateCopies ?? 0) > 0).length} /></div>
        <div className="operations-list">{profiles.map((profile) => <article key={profile.id}><div><strong>{profile.artifactPath}</strong><span>{profile.profileType}</span><small>{profile.issues.map((issue) => issue.message ?? issue.code).filter(Boolean).join(' · ') || 'No profile issues'}</small></div><code>{Object.keys(profile.metrics).length} metrics</code></article>)}</div>
      </SnapshotOperation> : null}

      {tab === 'impact' ? <SnapshotOperation title="Lineage and Impact Analysis" description="Connect source artifacts to archive membership, duplicate content, cross-file references, validation findings, cases, and derivative reports." disabled={!importId} actionLabel="Rebuild lineage" busy={busy === 'lineage'} onAction={() => importId && void execute('lineage', () => api.rebuildLineage(projectId, importId), 'Impact graph rebuilt.')}>
        <div className="operations-metrics"><Metric label="Edges" value={lineage.length} /><Metric label="Affected artifacts" value={affectedArtifacts} /><Metric label="Risk paths" value={lineage.filter((edge) => edge.edgeType === 'FindingEvidence').length} /></div>
        <div className="operations-list">{lineage.slice(0, 30).map((edge) => <article key={edge.id}><div><strong>{edge.fromPath}</strong><span>{edge.edgeType} → {edge.toPath ?? 'finding evidence'}</span><small>{edge.label}</small></div></article>)}</div>
      </SnapshotOperation> : null}

      {tab === 'playbooks' ? <div className="operations-grid"><section className="surface-card operations-panel"><span className="eyebrow">Repeatable investigation</span><h2>Starter playbook</h2><p>Profiles the snapshot, scans sensitive values, rebuilds impact paths, and prepares the review queue.</p><button className="primary-button" onClick={() => void execute('create-playbook', () => api.createPlaybook(projectId, { name: 'Evidence readiness review', description: 'Profile, classify, trace, and prepare evidence for human review.', steps: starterSteps }), 'Playbook created.')}>Create starter playbook</button></section><section className="surface-card operations-panel wide-panel"><div className="operations-list">{playbooks.map((playbook) => <article key={playbook.id}><div><strong>{playbook.name}</strong><span>{playbook.status} · {playbook.progressPercent}%</span><small>{playbook.lastRunSummary ?? playbook.description}</small></div><button className="primary-button" disabled={!importId || busy === `playbook-${playbook.id}`} onClick={() => importId && void execute(`playbook-${playbook.id}`, () => api.runPlaybook(projectId, playbook.id, importId), `${playbook.name} completed.`)}>Run</button></article>)}</div></section></div> : null}

      {tab === 'privacy' ? <SnapshotOperation title="Privacy and Redaction Center" description="Detect sensitive-value candidates locally and create redacted derivative ZIP packages while retaining immutable originals." disabled={!importId} actionLabel="Scan snapshot" busy={busy === 'privacy'} onAction={() => importId && void execute('privacy', () => api.runPrivacyScan(projectId, importId), 'Privacy scan completed.')}>
        <div className="operations-metrics"><Metric label="Candidates" value={privacy.length} /><Metric label="Restricted" value={privacy.filter((item) => item.severity === 'Restricted').length} /><Metric label="Kinds" value={privacyGroups.length} /></div>
        {importId ? <a className="primary-button inline-download" href={api.redactedExportUrl(projectId, importId)} download>Generate redacted package</a> : null}
        <div className="operations-list">{privacy.map((item) => <article key={item.id}><div><strong>{item.kind}</strong><span>{item.artifactPath} · {item.sourceLocation}</span><small>{item.maskedPreview} · {item.status}</small></div><div className="operation-actions"><button className="secondary-button" onClick={() => importId && void execute(`privacy-confirm-${item.id}`, () => api.updatePrivacyDetection(projectId, importId, item.id, 'Confirmed'), `${item.kind} confirmed.`)}>Confirm</button><button className="secondary-button" onClick={() => importId && void execute(`privacy-dismiss-${item.id}`, () => api.updatePrivacyDetection(projectId, importId, item.id, 'Dismissed'), `${item.kind} dismissed.`)}>Dismiss</button><code>{item.severity}</code></div></article>)}</div>
      </SnapshotOperation> : null}
    </div>
  );
}

function AgentSetup({ step, onBack, onNext }: { step: number; onBack: () => void; onNext: () => void }) {
  const steps = ['Discover agent', 'Verify version', 'Select workspace', 'Validate storage', 'Open project'];
  const descriptions = [
    'Probe approved localhost endpoints and confirm private-network browser permission.',
    'Confirm that the hosted shell and local agent use compatible major versions.',
    'Choose the local root that stores SQLite metadata, originals, extracted artifacts, and exports.',
    'Verify database writes, safe paths, original-file access, and available disk capacity.',
    'Restore the most recent project, snapshot, review queue, and investigation context.',
  ];
  return <div className="setup-grid"><aside className="surface-card setup-list">{steps.map((label, index) => <div key={label} className={`${index === step ? 'is-active' : ''}${index < step ? ' is-complete' : ''}`}><b>{index < step ? '✓' : index + 1}</b><span>{label}</span></div>)}</aside><section className="surface-card setup-detail"><span className="eyebrow">Step {step + 1} of {steps.length}</span><h2>{steps[step]}</h2><p>{descriptions[step]}</p><div className="recommendation"><strong>{step === 0 ? 'http://localhost:5087' : step === 1 ? 'Shell v6 ↔ Agent v6' : step === 2 ? '.workspace/' : step === 3 ? 'SQLite writable · 386 GB available' : 'Ready to restore workspace'}</strong></div><div className="modal-actions"><button className="secondary-button" onClick={onBack} disabled={step === 0}>Back</button><button className="primary-button" onClick={onNext}>{step === steps.length - 1 ? 'Open workspace' : 'Continue'}</button></div></section></div>;
}

function SnapshotOperation({ title, description, disabled, actionLabel, busy, onAction, children }: { title: string; description: string; disabled: boolean; actionLabel: string; busy: boolean; onAction: () => void; children: ReactNode }) {
  return <section className="surface-card operations-panel"><div className="section-heading"><div><span className="eyebrow">Current immutable snapshot</span><h2>{title}</h2><p>{description}</p></div><button className="primary-button" disabled={disabled || busy} onClick={onAction}>{busy ? 'Running…' : actionLabel}</button></div>{disabled ? <div className="empty-state"><h3>Select a snapshot</h3><p>This operation requires a current import snapshot.</p></div> : children}</section>;
}

function Metric({ label, value }: { label: string; value: number }) { return <div><span>{label}</span><strong>{value.toLocaleString()}</strong></div>; }
