import { useCallback, useEffect, useMemo, useState, type ChangeEvent, type FormEvent } from 'react';
import { api } from '../lib/api';
import { formatDate } from '../lib/format';
import type { AutomationRecipe, BaselinePolicy, EvidenceAnswer, ImportSummary, TriageItem } from '../lib/types';

interface DecisionCenterProps {
  projectId: string;
  importId: string | null;
  imports: ImportSummary[];
  onInspectArtifact: (artifactId: string) => void;
  onToast: (message: string) => void;
}

type DecisionTab = 'cockpit' | 'baselines' | 'automation' | 'assistant' | 'handoff';

const starterAutomationSteps = [
  { id: 'profile', name: 'Profile snapshot', type: 'profile', required: true, configuration: null },
  { id: 'privacy', name: 'Scan sensitive values', type: 'privacy', required: true, configuration: null },
  { id: 'impact', name: 'Rebuild lineage', type: 'impact', required: true, configuration: null },
  { id: 'baseline', name: 'Evaluate approved baseline', type: 'baseline', required: false, configuration: null },
  { id: 'triage', name: 'Rank review priorities', type: 'triage', required: true, configuration: null },
];

export function DecisionCenter({ projectId, importId, imports, onInspectArtifact, onToast }: DecisionCenterProps) {
  const [tab, setTab] = useState<DecisionTab>('cockpit');
  const [triage, setTriage] = useState<TriageItem[]>([]);
  const [baselines, setBaselines] = useState<BaselinePolicy[]>([]);
  const [recipes, setRecipes] = useState<AutomationRecipe[]>([]);
  const [answer, setAnswer] = useState<EvidenceAnswer | null>(null);
  const [question, setQuestion] = useState('Which artifacts need attention and why?');
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [baselineRows, recipeRows] = await Promise.all([api.getBaselines(projectId), api.getAutomationRecipes(projectId)]);
      setBaselines(baselineRows);
      setRecipes(recipeRows);
      setTriage(importId ? await api.getTriage(projectId, importId) : []);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not load decision operations.');
    }
  }, [projectId, importId]);

  useEffect(() => { void load(); }, [load]);

  async function execute(key: string, action: () => Promise<unknown>, success: string) {
    setBusy(key); setError(null);
    try { await action(); onToast(success); await load(); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Decision operation failed.'); }
    finally { setBusy(null); }
  }

  async function createBaseline() {
    if (!importId) return;
    await execute('create-baseline', () => api.createBaseline(projectId, {
      name: `Approved baseline · ${imports.find((item) => item.id === importId)?.displayName ?? 'current snapshot'}`,
      baselineImportId: importId,
    }), 'Baseline policy created from the selected snapshot.');
  }

  async function createRecipe() {
    await execute('create-recipe', () => api.createAutomationRecipe(projectId, {
      name: 'Snapshot decision readiness', description: 'Profile, classify, trace, evaluate, and rank every new snapshot.',
      steps: starterAutomationSteps, triggerMode: 'OnSnapshot', scheduleIntervalMinutes: 1440,
    }), 'Decision-readiness automation created.');
  }

  async function ask(event: FormEvent) {
    event.preventDefault();
    if (!importId || question.trim().length < 3) return;
    setBusy('assistant'); setError(null);
    try { setAnswer(await api.askEvidence(projectId, importId, question)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Evidence question failed.'); }
    finally { setBusy(null); }
  }

  const critical = triage.filter((item) => item.priorityBand === 'Critical').length;
  const high = triage.filter((item) => item.priorityBand === 'High').length;
  const averageScore = triage.length ? Math.round(triage.reduce((sum, item) => sum + item.priorityScore, 0) / triage.length) : 0;
  const latestBaseline = baselines.find((item) => item.lastEvaluatedImportId === importId) ?? baselines[0] ?? null;
  const topFactors = useMemo(() => {
    const counts = new Map<string, number>();
    for (const item of triage.slice(0, 20)) for (const factor of item.factors) counts.set(factor.name, (counts.get(factor.name) ?? 0) + factor.points);
    return [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 5);
  }, [triage]);

  return (
    <div className="decision-center">
      <div className="operations-tabs" role="tablist">
        {([
          ['cockpit', 'Decision cockpit'], ['baselines', 'Baselines'], ['automation', 'Automation'], ['assistant', 'Evidence assistant'], ['handoff', 'Handoff'],
        ] as Array<[DecisionTab, string]>).map(([id, label]) => <button key={id} type="button" className={tab === id ? 'is-active' : ''} onClick={() => setTab(id)}>{label}</button>)}
      </div>
      {error ? <div className="global-error" role="alert">{error}</div> : null}

      {tab === 'cockpit' ? <div className="page-stack">
        <section className="metrics-grid">
          <DecisionMetric label="Critical priorities" value={critical} detail="Immediate review" tone="danger" />
          <DecisionMetric label="High priorities" value={high} detail="Review next" tone="warning" />
          <DecisionMetric label="Average priority" value={averageScore} detail="Transparent weighted score" />
          <DecisionMetric label="Baseline status" value={latestBaseline?.status ?? 'Not configured'} detail={latestBaseline?.lastEvaluatedAtUtc ? `Evaluated ${formatDate(latestBaseline.lastEvaluatedAtUtc)}` : 'Create an approved baseline'} tone={latestBaseline?.status === 'Regressed' ? 'danger' : latestBaseline?.status === 'Improved' || latestBaseline?.status === 'Passed' ? 'success' : 'default'} />
        </section>
        <div className="overview-grid">
          <section className="surface-card operations-panel wide-panel">
            <div className="section-heading"><div><span className="eyebrow">Transparent prioritization</span><h2>Smart triage inbox</h2></div><button className="secondary-button" onClick={() => void load()}>Recalculate</button></div>
            <div className="decision-list">{triage.slice(0, 30).map((item) => <button key={item.artifactId} type="button" className="decision-row" onClick={() => onInspectArtifact(item.artifactId)}><span className={`priority-score priority-${item.priorityBand.toLowerCase()}`}>{item.priorityScore}</span><span><strong>{item.artifactPath}</strong><small>{item.reviewStatus} · {item.findingCount} findings · {item.impactCount} impact edges · {item.privacyCount} privacy candidates</small></span><span className="factor-tags">{item.factors.slice(0, 3).map((factor) => <i key={factor.name}>{factor.name} +{factor.points}</i>)}</span></button>)}</div>
          </section>
          <section className="surface-card operations-panel">
            <span className="eyebrow">Why work is prioritized</span><h2>Dominant risk factors</h2>
            <div className="factor-summary">{topFactors.map(([name, points]) => <div key={name}><span>{name}</span><strong>{points}</strong><i style={{ width: `${Math.min(100, points)}%` }} /></div>)}</div>
            <p className="quiet-copy">Priority scores are advisory. Every point is explained and linked to evidence; no opaque model output is treated as fact.</p>
          </section>
        </div>
      </div> : null}

      {tab === 'baselines' ? <div className="operations-grid">
        <section className="surface-card operations-panel"><span className="eyebrow">Approved regression contract</span><h2>Create baseline</h2><p>Capture accepted thresholds for errors, warnings, parser failures, unsupported content, and expected artifact count from the selected immutable snapshot.</p><button className="primary-button" disabled={!importId || busy === 'create-baseline'} onClick={() => void createBaseline()}>{busy === 'create-baseline' ? 'Creating…' : 'Approve current snapshot as baseline'}</button></section>
        <section className="surface-card operations-panel wide-panel"><div className="section-heading"><div><span className="eyebrow">Policies</span><h2>{baselines.length} baselines</h2></div></div><div className="operations-list">{baselines.map((baseline) => <article key={baseline.id}><div><strong>{baseline.name}</strong><span>{baseline.status} · {baseline.rules.length} rules</span><small>{baseline.lastEvaluatedAtUtc ? `Last evaluated ${formatDate(baseline.lastEvaluatedAtUtc)}` : 'Not yet evaluated'}</small></div><button className="primary-button" disabled={!importId || busy === `baseline-${baseline.id}`} onClick={() => importId && void execute(`baseline-${baseline.id}`, () => api.evaluateBaseline(projectId, baseline.id, importId), `${baseline.name} evaluated.`)}>Evaluate current</button></article>)}</div></section>
      </div> : null}

      {tab === 'automation' ? <div className="operations-grid">
        <section className="surface-card operations-panel"><span className="eyebrow">Governed orchestration</span><h2>Decision-readiness recipe</h2><p>Automatically profile, scan privacy, rebuild impact, evaluate baselines, and rank priorities for every snapshot.</p><button className="primary-button" onClick={() => void createRecipe()} disabled={busy === 'create-recipe'}>Create starter automation</button></section>
        <section className="surface-card operations-panel wide-panel"><div className="operations-list">{recipes.map((recipe) => <article key={recipe.id}><div><strong>{recipe.name}</strong><span>{recipe.triggerMode} · {recipe.status} · {recipe.progressPercent}%</span><small>{recipe.lastRunSummary ?? recipe.description}</small></div><div className="operation-actions"><button className="secondary-button" onClick={() => void execute(`toggle-recipe-${recipe.id}`, () => api.updateAutomationRecipe(projectId, recipe.id, { enabled: !recipe.enabled }), `${recipe.name} ${recipe.enabled ? 'paused' : 'enabled'}.`)}>{recipe.enabled ? 'Pause' : 'Enable'}</button><button className="primary-button" disabled={!importId || busy === `recipe-${recipe.id}`} onClick={() => importId && void execute(`recipe-${recipe.id}`, () => api.runAutomationRecipe(projectId, recipe.id, importId), `${recipe.name} completed.`)}>Run now</button></div></article>)}</div></section>
      </div> : null}

      {tab === 'assistant' ? <div className="operations-grid">
        <section className="surface-card operations-panel"><span className="eyebrow">Citation-first investigation</span><h2>Ask the selected snapshot</h2><p>The assistant searches local artifact metadata, bounded previews, and validation evidence. Every response includes source citations and confidence.</p><form className="operations-form" onSubmit={(event: FormEvent) => void ask(event)}><label>Evidence question<textarea value={question} onChange={(event: ChangeEvent<HTMLTextAreaElement>) => setQuestion(event.target.value)} rows={5} /></label><button className="primary-button" disabled={!importId || busy === 'assistant'}>{busy === 'assistant' ? 'Searching…' : 'Ask evidence'}</button></form></section>
        <section className="surface-card operations-panel wide-panel">{answer ? <><div className="section-heading"><div><span className="eyebrow">Grounded answer</span><h2>Confidence: {answer.confidence}</h2></div></div><p className="assistant-answer">{answer.answer}</p><div className="citation-list">{answer.citations.map((citation, index) => <button type="button" key={`${citation.artifactId}-${citation.findingId}-${index}`} onClick={() => citation.artifactId && onInspectArtifact(citation.artifactId)}><b>{index + 1}</b><span><strong>{citation.artifactPath}</strong><small>{citation.sourceLocation ?? citation.basis}</small><p>{citation.excerpt}</p></span></button>)}</div></> : <div className="empty-state"><h2>No evidence question run</h2><p>Ask about missing values, duplicate identifiers, changed mappings, parser failures, privacy candidates, or review priorities.</p></div>}</section>
      </div> : null}

      {tab === 'handoff' ? <div className="operations-grid">
        <section className="surface-card operations-panel"><span className="eyebrow">Portable decision record</span><h2>Decision brief</h2><p>Package the selected snapshot identity, triage priorities, findings, baseline evaluations, profiles, and provenance into a portable ZIP for review or handoff.</p>{importId ? <a className="primary-button" href={api.decisionBriefUrl(projectId, importId)} download>Generate decision brief</a> : <button className="primary-button" disabled>Select a snapshot</button>}</section>
        <section className="surface-card operations-panel wide-panel"><span className="eyebrow">Included records</span><h2>Handoff manifest</h2><div className="handoff-grid">{['Immutable snapshot identity','Transparent triage factors','Validation findings and source locations','Baseline policy results','Data-quality profiles','SHA-256 provenance and generation timestamp'].map((item) => <div key={item}><span>✓</span><strong>{item}</strong></div>)}</div><p className="quiet-copy">The package does not replace original evidence. Recipients should reopen cited artifacts in Workbench Studio before approving a conclusion.</p></section>
      </div> : null}
    </div>
  );
}

function DecisionMetric({ label, value, detail, tone = 'default' }: { label: string; value: string | number; detail: string; tone?: 'default' | 'success' | 'warning' | 'danger' }) {
  return <article className={`metric-card tone-${tone}`}><span>{label}</span><strong>{value}</strong><small>{detail}</small></article>;
}
