import { useCallback, useEffect, useMemo, useState } from 'react';
import { api } from '../lib/api';
import { formatDate } from '../lib/format';
import type {
  AdaptiveQueueItem, AnomalyExplanation, ApprovalGate, ExecutiveSummary, ImportSummary, QueuePolicy, ScenarioRun,
} from '../lib/types';

interface CommandIntelligenceCenterProps {
  projectId: string;
  importId: string | null;
  imports: ImportSummary[];
  onInspectArtifact: (artifactId: string) => void;
  onToast: (message: string) => void;
}

type CommandTab = 'queue' | 'scenarios' | 'approvals' | 'explain' | 'executive';

const scenarioPresets = [
  { name: 'Resolve blocking errors', assumptions: [{ metric: 'errorCount', delta: -2, description: 'Resolve the two current error findings.' }] },
  { name: 'Privacy-safe release', assumptions: [{ metric: 'privacyOpenCount', delta: -4, description: 'Review and clear all open privacy candidates.' }] },
  { name: 'Full readiness plan', assumptions: [
    { metric: 'errorCount', delta: -2, description: 'Resolve error findings.' },
    { metric: 'warningCount', delta: -5, description: 'Resolve the highest-value warning cohort.' },
    { metric: 'privacyOpenCount', delta: -4, description: 'Clear privacy candidates.' },
    { metric: 'parseFailureCount', delta: -1, description: 'Recover the failed parser.' },
  ] },
];

export function CommandIntelligenceCenter({ projectId, importId, imports, onInspectArtifact, onToast }: CommandIntelligenceCenterProps) {
  const [tab, setTab] = useState<CommandTab>('queue');
  const [policies, setPolicies] = useState<QueuePolicy[]>([]);
  const [queue, setQueue] = useState<AdaptiveQueueItem[]>([]);
  const [scenarios, setScenarios] = useState<ScenarioRun[]>([]);
  const [gates, setGates] = useState<ApprovalGate[]>([]);
  const [explanations, setExplanations] = useState<AnomalyExplanation[]>([]);
  const [summary, setSummary] = useState<ExecutiveSummary | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [policyRows, scenarioRows, gateRows] = await Promise.all([
        api.getQueuePolicies(projectId), api.getScenarios(projectId), api.getApprovalGates(projectId),
      ]);
      setPolicies(policyRows); setScenarios(scenarioRows); setGates(gateRows);
      if (importId) {
        const [queueRows, explanationRows, executive] = await Promise.all([
          api.getAdaptiveQueue(projectId, importId), api.getAnomalyExplanations(projectId, importId), api.getExecutiveSummary(projectId, importId),
        ]);
        setQueue(queueRows); setExplanations(explanationRows); setSummary(executive);
      } else { setQueue([]); setExplanations([]); setSummary(null); }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not load command intelligence.');
    }
  }, [projectId, importId]);

  useEffect(() => { void load(); }, [load]);

  async function execute(key: string, action: () => Promise<unknown>, success: string) {
    setBusy(key); setError(null);
    try { await action(); onToast(success); await load(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Command operation failed.'); }
    finally { setBusy(null); }
  }

  async function createPolicy() {
    await execute('policy', () => api.createQueuePolicy(projectId, { name: 'Adaptive command queue', weights: null, slaHours: 24, active: true }), 'Adaptive queue policy created.');
  }

  async function runScenario(preset: typeof scenarioPresets[number]) {
    if (!importId) return;
    await execute(`scenario-${preset.name}`, () => api.runScenario(projectId, { name: preset.name, importId, assumptions: preset.assumptions }), `${preset.name} simulated.`);
  }

  async function createGate() {
    if (!importId) return;
    await execute('gate', () => api.createApprovalGate(projectId, { name: `Release gate · ${imports.find(x => x.id === importId)?.displayName ?? 'current snapshot'}`, importId, gateType: 'Release', requiredRole: 'Evidence approver' }), 'Approval gate created.');
  }

  const critical = queue.filter(item => item.band === 'Critical').length;
  const overdue = queue.filter(item => item.slaState === 'Overdue').length;
  const pendingGates = gates.filter(item => item.importSnapshotId === importId && item.status === 'Pending').length;
  const activePolicy = policies.find(item => item.active) ?? policies[0] ?? null;
  const latestScenarios = useMemo(() => scenarios.filter(item => item.importSnapshotId === importId).slice(0, 8), [scenarios, importId]);

  return <div className="command-intelligence-center">
    <div className="operations-tabs" role="tablist">
      {([['queue', 'Adaptive queue'], ['scenarios', 'Scenario lab'], ['approvals', 'Approval gates'], ['explain', 'Explainability'], ['executive', 'Executive briefing']] as Array<[CommandTab, string]>).map(([id, label]) =>
        <button key={id} type="button" className={tab === id ? 'is-active' : ''} onClick={() => setTab(id)}>{label}</button>)}
    </div>
    {error ? <div className="global-error" role="alert">{error}</div> : null}

    {tab === 'queue' ? <div className="page-stack">
      <section className="metrics-grid">
        <CommandMetric label="Critical queue" value={critical} detail="Highest command priority" tone="danger" />
        <CommandMetric label="SLA overdue" value={overdue} detail={`${activePolicy?.slaHours ?? 24}-hour policy window`} tone={overdue ? 'warning' : 'success'} />
        <CommandMetric label="Queue depth" value={queue.length} detail="Adaptive ranked artifacts" />
        <CommandMetric label="Active policy" value={activePolicy?.name ?? 'Default'} detail={activePolicy ? `${activePolicy.weights.length} weighted factors` : 'Built-in transparent weights'} />
      </section>
      <div className="overview-grid">
        <section className="surface-card operations-panel wide-panel">
          <div className="section-heading"><div><span className="eyebrow">Adaptive command queue</span><h2>Ranked by risk, urgency, and review state</h2></div><button className="secondary-button" onClick={() => void load()}>Recalculate</button></div>
          <div className="decision-list">{queue.slice(0, 50).map(item => <button key={item.artifactId} type="button" className="decision-row adaptive-row" onClick={() => onInspectArtifact(item.artifactId)}>
            <span className={`priority-score priority-${item.band.toLowerCase()}`}>{item.rank}</span>
            <span><strong>{item.artifactPath}</strong><small>Score {item.score} · {item.reviewStatus} · due {formatDate(item.dueAtUtc)}</small></span>
            <span className={`command-sla sla-${item.slaState.toLowerCase()}`}>{item.slaState}</span>
            <span className="factor-tags">{item.reasons.slice(0, 3).map(reason => <i key={reason.name}>{reason.name} +{reason.points}</i>)}</span>
          </button>)}</div>
        </section>
        <section className="surface-card operations-panel"><span className="eyebrow">Queue policy</span><h2>{activePolicy?.name ?? 'Built-in default'}</h2><p className="quiet-copy">Every rank is reconstructed from visible multipliers, review state, and SLA pressure.</p>{activePolicy ? <div className="policy-weight-list">{activePolicy.weights.map(weight => <div key={weight.metric}><span>{weight.metric}</span><strong>{weight.multiplier.toFixed(2)}×</strong></div>)}</div> : <button className="primary-button" disabled={busy === 'policy'} onClick={() => void createPolicy()}>Create persisted queue policy</button>}</section>
      </div>
    </div> : null}

    {tab === 'scenarios' ? <div className="operations-grid">
      <section className="surface-card operations-panel"><span className="eyebrow">What-if simulation</span><h2>Readiness scenario presets</h2><p>Model potential remediation outcomes without mutating findings, reviews, baselines, or original evidence.</p><div className="scenario-preset-list">{scenarioPresets.map(preset => <button key={preset.name} className="secondary-button" disabled={!importId || busy === `scenario-${preset.name}`} onClick={() => void runScenario(preset)}>{preset.name}</button>)}</div></section>
      <section className="surface-card operations-panel wide-panel"><div className="section-heading"><div><span className="eyebrow">Scenario history</span><h2>{latestScenarios.length} simulations</h2></div></div><div className="scenario-grid">{latestScenarios.map(item => <article key={item.id} className="scenario-card"><span className="eyebrow">{formatDate(item.createdAtUtc)}</span><h3>{item.name}</h3><div className="scenario-score"><strong>{item.result.currentReadinessScore}</strong><span>→</span><strong>{item.result.projectedReadinessScore}</strong><i>+{item.result.scoreDelta}</i></div><p>{item.result.projectedStatus}</p><ul>{item.result.recommendations.slice(0, 3).map(x => <li key={x}>{x}</li>)}</ul></article>)}</div></section>
    </div> : null}

    {tab === 'approvals' ? <div className="operations-grid">
      <section className="surface-card operations-panel"><span className="eyebrow">Formal decision control</span><h2>Create approval gate</h2><p>Evaluate explicit readiness requirements before a reviewer can approve release, distribution, or evidence handoff.</p><button className="primary-button" disabled={!importId || busy === 'gate'} onClick={() => void createGate()}>Create release gate</button></section>
      <section className="surface-card operations-panel wide-panel"><div className="section-heading"><div><span className="eyebrow">Current snapshot</span><h2>{pendingGates} pending gates</h2></div></div><div className="approval-list">{gates.filter(gate => gate.importSnapshotId === importId).map(gate => <article key={gate.id} className="approval-card"><div><span className={`badge ${gate.status === 'Approved' ? 'good' : gate.status === 'Rejected' ? 'bad' : 'warn'}`}>{gate.status}</span><h3>{gate.name}</h3><small>{gate.gateType} · required role: {gate.requiredRole}</small></div><div className="approval-requirements">{gate.requirements.map(req => <div key={req.name}><b>{req.passed ? '✓' : '!'}</b><span><strong>{req.name}</strong><small>{req.evidence}</small></span></div>)}</div>{gate.status === 'Pending' ? <div className="operation-actions"><button className="secondary-button" onClick={() => void execute(`reject-${gate.id}`, () => api.decideApprovalGate(projectId, gate.id, { decision: 'Reject', decidedBy: 'Local reviewer', rationale: 'Gate rejected pending remediation.' }), 'Gate rejected.')}>Reject</button><button className="primary-button" disabled={gate.requirements.some(req => !req.passed)} onClick={() => void execute(`approve-${gate.id}`, () => api.decideApprovalGate(projectId, gate.id, { decision: 'Approve', decidedBy: 'Local reviewer', rationale: 'All deterministic requirements passed.' }), 'Gate approved.')}>Approve</button></div> : <small>Decided by {gate.decidedBy ?? 'reviewer'} {gate.decidedAtUtc ? formatDate(gate.decidedAtUtc) : ''}</small>}</article>)}</div></section>
    </div> : null}

    {tab === 'explain' ? <div className="page-stack"><section className="surface-card operations-panel"><div className="section-heading"><div><span className="eyebrow">Anomaly explainability</span><h2>Observed → expected → drivers → impact</h2></div><span className="chip">{explanations.length} explanation groups</span></div><div className="explanation-grid">{explanations.map(item => <article key={`${item.artifactId}-${item.title}`} className="explanation-card"><header><span className={`badge ${item.severity === 'Critical' ? 'bad' : 'warn'}`}>{item.severity}</span><button className="text-button" onClick={() => item.artifactId && onInspectArtifact(item.artifactId)}>{item.artifactPath}</button></header><h3>{item.title}</h3><dl><div><dt>Observed</dt><dd>{item.observed}</dd></div><div><dt>Expected</dt><dd>{item.expected}</dd></div><div><dt>Impact</dt><dd>{item.impact}</dd></div><div><dt>Next action</dt><dd>{item.recommendedAction}</dd></div></dl><div className="factor-tags">{item.drivers.map(driver => <i key={driver}>{driver}</i>)}</div></article>)}</div></section></div> : null}

    {tab === 'executive' ? <div className="page-stack">{summary ? <>
      <section className="executive-hero surface-card"><div><span className="eyebrow">Executive decision briefing</span><h2>{summary.status}</h2><p>{summary.highlights.join(' ')}</p></div><div className={`executive-score status-${summary.status.toLowerCase().replace(' ', '-')}`}><strong>{summary.readinessScore}</strong><span>readiness</span></div></section>
      <section className="metrics-grid"><CommandMetric label="Critical priorities" value={summary.criticalPriorities} detail="Immediate command attention" tone="danger" /><CommandMetric label="Pending approvals" value={summary.pendingApprovals} detail="Formal gates" tone="warning" /><CommandMetric label="Regressed policies" value={summary.regressedPolicies} detail="Against approved baselines" tone={summary.regressedPolicies ? 'danger' : 'success'} /><CommandMetric label="Queue items" value={summary.queueItems} detail="Ranked evidence" /></section>
      <div className="overview-grid"><section className="surface-card operations-panel wide-panel"><span className="eyebrow">Top decision priorities</span><h2>What leadership needs to know</h2><div className="decision-list">{summary.topPriorities.map(item => <div key={item.rank} className="decision-row"><span className={`priority-score priority-${item.band.toLowerCase()}`}>{item.rank}</span><span><strong>{item.artifactPath}</strong><small>{item.drivers.join(' · ')}</small></span><strong>{item.score}</strong></div>)}</div></section><section className="surface-card operations-panel"><span className="eyebrow">Portable reporting</span><h2>Executive brief package</h2><p>Download an HTML leadership summary plus queue, scenario, approval, and metric records.</p><a className="primary-button" href={api.executiveBriefUrl(projectId, importId!)} download>Generate executive brief</a></section></div>
    </> : <div className="empty-state"><h2>No snapshot selected</h2><p>Select an immutable snapshot to generate the executive briefing.</p></div>}</div> : null}
  </div>;
}

function CommandMetric({ label, value, detail, tone = 'default' }: { label: string; value: string | number; detail: string; tone?: 'default' | 'danger' | 'warning' | 'success' }) {
  return <article className={`metric-card command-metric tone-${tone}`}><span>{label}</span><strong>{typeof value === 'number' ? value.toLocaleString() : value}</strong><small>{detail}</small></article>;
}
