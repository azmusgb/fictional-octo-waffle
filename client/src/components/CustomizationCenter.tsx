import {
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type ChangeEvent,
  type MouseEvent as ReactMouseEvent,
} from 'react';
import type { ViewId } from '../lib/types';
import {
  ACCENT_COLORS,
  PREFERENCE_CATEGORIES,
  applyDisplayProfile,
  applyPreferencesPackage,
  applyRolePreset,
  applyWorkspaceProfile,
  createPreferencesPackage,
  createWorkspaceProfile,
  deleteWorkspaceProfile,
  getPreferenceDiagnostics,
  markPreferencesExported,
  parsePreferencesPackage,
  resetPreferenceCategory,
  restartOnboarding,
  restorePreferenceBackup,
  serializePreferenceDiagnostics,
  serializePreferencesPackage,
  updateWorkspaceProfile,
  type AlertRuleId,
  type DashboardWidgetId,
  type DisplayProfileId,
  type PreferenceCategory,
  type PreferencesPackage,
  type QuickActionId,
  type UserPreferences,
  type WorkspaceRoleId,
} from '../lib/userPreferences';

interface NavigationOption {
  id: ViewId;
  label: string;
  symbol: string;
  description: string;
}

interface CustomizationCenterProps {
  open: boolean;
  preferences: UserPreferences;
  navigationOptions: NavigationOption[];
  onChange: (preferences: UserPreferences) => void;
  onClose: () => void;
  onReset: () => void;
}

type CenterTab = 'guide' | 'customize' | 'profiles' | 'transfer' | 'recovery';

const centerTabMeta: Record<CenterTab, { label: string; detail: string; symbol: string }> = {
  guide: { label: 'Guided setup', detail: 'Start from your role', symbol: '◎' },
  customize: { label: 'Customize', detail: 'Shape the workspace', symbol: '✦' },
  profiles: { label: 'Profiles', detail: 'Save working modes', symbol: '▣' },
  transfer: { label: 'Transfer', detail: 'Move settings safely', symbol: '⇱' },
  recovery: { label: 'Recovery', detail: 'Diagnose and restore', symbol: '↶' },
};

const dashboardLabels: Record<DashboardWidgetId, string> = {
  metrics: 'Readiness metrics',
  processing: 'Active processing',
  intake: 'Imports and snapshot history',
  insights: 'Workspace insights',
};

const profileLabels: Record<DisplayProfileId, { title: string; detail: string }> = {
  standard: { title: 'Standard', detail: 'Balanced density and motion.' },
  'large-touch': { title: 'Large Touch', detail: 'Larger text and comfortable controls.' },
  'high-contrast': { title: 'High Contrast', detail: 'Stronger boundaries and reduced motion.' },
  'dense-review': { title: 'Dense Review', detail: 'Compact layouts for high-volume queues.' },
  'evidence-reading': { title: 'Evidence Reading', detail: 'Larger source text and reduced distraction.' },
};

const alertLabels: Record<AlertRuleId, string> = {
  criticalFinding: 'Critical finding detected',
  approvalAssigned: 'Approval assigned to me',
  privacyDetected: 'Privacy review required',
  baselineRegression: 'Baseline regression detected',
  automationFailed: 'Automation failed',
  agentDisconnected: 'Local agent disconnected',
};

const quickActionLabels: Record<QuickActionId, string> = {
  review: 'Open review queue',
  findings: 'Open findings',
  compare: 'Compare snapshots',
  command: 'Open command palette',
  operations: 'Open operations',
  exports: 'Open exports',
  customize: 'Open customization',
};

const categoryLabels: Record<PreferenceCategory, { title: string; detail: string }> = {
  navigation: { title: 'Navigation', detail: 'Mobile tabs and order' },
  dashboard: { title: 'Dashboard', detail: 'Widget visibility and order' },
  display: { title: 'Display', detail: 'Theme, density, text, contrast, and accent' },
  queue: { title: 'Queues', detail: 'Start view, finding severity, and sorting' },
  alerts: { title: 'Alerts', detail: 'Delivery mode, quiet hours, and event rules' },
  quickActions: { title: 'Quick actions', detail: 'Safe mobile shortcuts' },
  profiles: { title: 'Named profiles', detail: 'Custom profile definitions' },
};

const roleLabels: Record<WorkspaceRoleId, { title: string; detail: string }> = {
  investigator: { title: 'Investigator', detail: 'Evidence reading, findings, and comparison.' },
  reviewer: { title: 'Reviewer', detail: 'High-volume review, findings, and queue control.' },
  approver: { title: 'Approver', detail: 'Baselines, decision readiness, and governed exports.' },
  operations: { title: 'Operations', detail: 'Watch folders, ingestion, profiles, and mobile action.' },
  executive: { title: 'Executive', detail: 'Readiness, priorities, and concise decision delivery.' },
};

function moveItem<T>(items: T[], index: number, direction: -1 | 1): T[] {
  const nextIndex = index + direction;
  if (nextIndex < 0 || nextIndex >= items.length) return items;
  const next = [...items];
  const current = next[index];
  const target = next[nextIndex];
  if (current === undefined || target === undefined) return items;
  next[index] = target;
  next[nextIndex] = current;
  return next;
}

function downloadText(filename: string, text: string, type = 'application/json') {
  const blob = new Blob([text], { type });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

export function CustomizationCenter({ open, preferences, navigationOptions, onChange, onClose, onReset }: CustomizationCenterProps) {
  const [tab, setTab] = useState<CenterTab>(() => preferences.onboarding.complete ? 'customize' : 'guide');
  const [message, setMessage] = useState<string | null>(null);
  const [profileName, setProfileName] = useState('');
  const [profileDescription, setProfileDescription] = useState('');
  const [deviceLabel, setDeviceLabel] = useState(() => navigator.userAgent.includes('iPhone') ? 'My iPhone' : 'My Workbench device');
  const [exportCategories, setExportCategories] = useState<PreferenceCategory[]>([...PREFERENCE_CATEGORIES]);
  const [importPackage, setImportPackage] = useState<PreferencesPackage | null>(null);
  const [importCategories, setImportCategories] = useState<PreferenceCategory[]>([]);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const diagnostics = useMemo(() => getPreferenceDiagnostics(preferences), [preferences]);

  if (!open) return null;

  function update(next: UserPreferences, feedback?: string) {
    onChange(next);
    if (feedback) setMessage(feedback);
  }

  function toggleMobileTab(id: ViewId) {
    const selected = preferences.mobileTabs.includes(id);
    if (selected) {
      if (preferences.mobileTabs.length === 1) return;
      update({ ...preferences, activeProfileId: null, mobileTabs: preferences.mobileTabs.filter((item) => item !== id) });
      return;
    }
    if (preferences.mobileTabs.length >= 4) return;
    update({ ...preferences, activeProfileId: null, mobileTabs: [...preferences.mobileTabs, id] });
  }

  function toggleWidget(id: DashboardWidgetId) {
    const hidden = preferences.dashboard.hidden.includes(id);
    update({
      ...preferences,
      activeProfileId: null,
      dashboard: {
        ...preferences.dashboard,
        hidden: hidden ? preferences.dashboard.hidden.filter((item) => item !== id) : [...preferences.dashboard.hidden, id],
      },
    });
  }

  function toggleQuickAction(id: QuickActionId) {
    const selected = preferences.quickActions.includes(id);
    if (!selected && preferences.quickActions.length >= 4) return;
    update({
      ...preferences,
      activeProfileId: null,
      quickActions: selected ? preferences.quickActions.filter((item) => item !== id) : [...preferences.quickActions, id],
    });
  }

  function toggleCategory(category: PreferenceCategory, selected: PreferenceCategory[], setSelected: (value: PreferenceCategory[]) => void) {
    setSelected(selected.includes(category) ? selected.filter((item) => item !== category) : [...selected, category]);
  }

  async function exportPreferences(useShareSheet: boolean) {
    if (exportCategories.length === 0) {
      setMessage('Select at least one preference category to export.');
      return;
    }
    const pkg = createPreferencesPackage(preferences, exportCategories, deviceLabel);
    const text = serializePreferencesPackage(pkg);
    const filename = `workbench-preferences-${new Date().toISOString().slice(0, 10)}.json`;
    const file = new File([text], filename, { type: 'application/json' });
    if (useShareSheet && navigator.share && (!navigator.canShare || navigator.canShare({ files: [file] }))) {
      try {
        await navigator.share({ title: 'Workbench Studio preferences', text: 'Portable Workbench Studio v8.4 preference package.', files: [file] });
        update(markPreferencesExported(preferences), 'Preference package shared.');
        return;
      } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') return;
      }
    }
    downloadText(filename, text);
    update(markPreferencesExported(preferences), 'Preference package downloaded.');
  }

  async function copyPreferences() {
    const pkg = createPreferencesPackage(preferences, exportCategories, deviceLabel);
    await navigator.clipboard.writeText(serializePreferencesPackage(pkg));
    update(markPreferencesExported(preferences), 'Preference package copied to the clipboard.');
  }

  async function readImport(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    try {
      const parsed = parsePreferencesPackage(await file.text());
      setImportPackage(parsed);
      setImportCategories([...parsed.categories]);
      setMessage(`Validated package from ${parsed.deviceLabel}. Review the categories before applying.`);
    } catch (error) {
      setImportPackage(null);
      setMessage(error instanceof Error ? error.message : 'The preference package could not be read.');
    }
  }

  function applyImport() {
    if (!importPackage || importCategories.length === 0) return;
    update(applyPreferencesPackage(preferences, importPackage, importCategories), `Imported ${importCategories.length} preference categories from ${importPackage.deviceLabel}.`);
    setImportPackage(null);
    setImportCategories([]);
  }

  function createProfile() {
    if (!profileName.trim()) {
      setMessage('Enter a profile name first.');
      return;
    }
    update(createWorkspaceProfile(preferences, profileName, profileDescription), `Saved profile “${profileName.trim()}”.`);
    setProfileName('');
    setProfileDescription('');
  }

  function applyRole(role: WorkspaceRoleId) {
    update(applyRolePreset(preferences, role), `${roleLabels[role].title} workspace applied. You can refine any setting.`);
    setTab('customize');
  }

  function restoreBackup() {
    const restored = restorePreferenceBackup();
    if (!restored) {
      setMessage('No usable preference backup is available.');
      return;
    }
    update(restored, 'Restored the last known good preference backup.');
  }

  return (
    <div className="customization-backdrop" role="presentation" onMouseDown={(event: ReactMouseEvent<HTMLDivElement>) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className="customization-panel portable-workspace-panel" role="dialog" aria-modal="true" aria-labelledby="customization-heading">
        <header className="customization-header">
          <div><span className="eyebrow">Portable workspace · v8.4 experience</span><h2 id="customization-heading">Your workspace, shaped around the decision</h2><p>Personalize presentation, move selected settings between devices, and recover safely. Evidence, findings, policies, approvals, and audit records remain governed and separate.</p></div>
          <button type="button" className="icon-button" aria-label="Close customization" onClick={onClose}>×</button>
        </header>

        <div className="portable-summary-strip" aria-label="Preference state summary">
          <div><span>Active profile</span><strong>{preferences.profiles.find((item) => item.id === preferences.activeProfileId)?.name ?? 'Custom workspace'}</strong></div>
          <div><span>Guided setup</span><strong>{preferences.onboarding.complete ? 'Complete' : 'Recommended next'}</strong></div>
          <div><span>Last transfer</span><strong>{preferences.portability.lastImportAt ? new Date(preferences.portability.lastImportAt).toLocaleDateString() : preferences.portability.lastExportAt ? new Date(preferences.portability.lastExportAt).toLocaleDateString() : 'Not yet transferred'}</strong></div>
          <div><span>Recovery</span><strong>{diagnostics.backupAvailable ? 'Backup available' : 'No backup yet'}</strong></div>
        </div>

        <nav className="portable-tabs" aria-label="Personal workspace sections">
          {(Object.keys(centerTabMeta) as CenterTab[]).map((id) => <button key={id} type="button" className={tab === id ? 'is-active' : ''} onClick={() => setTab(id)} aria-current={tab === id ? 'page' : undefined}><i aria-hidden="true">{centerTabMeta[id].symbol}</i><strong>{centerTabMeta[id].label}</strong><small>{centerTabMeta[id].detail}</small></button>)}
        </nav>

        {message ? <div className="portable-message" role="status"><span>{message}</span><button type="button" onClick={() => setMessage(null)} aria-label="Dismiss message">×</button></div> : null}

        <div className="customization-content">
          {tab === 'guide' ? <>
            <section className="custom-section guide-hero">
              <span className="eyebrow">Three-step setup</span>
              <h3>Start with the work you perform</h3>
              <p>Choose a role preset. It configures mobile navigation, dashboard emphasis, display profile, queue defaults, alerts, and safe quick actions. Every setting remains editable.</p>
              <div className="role-preset-grid">
                {(Object.keys(roleLabels) as WorkspaceRoleId[]).map((role) => <button key={role} type="button" className={preferences.onboarding.role === role ? 'is-selected' : ''} onClick={() => applyRole(role)}><strong>{roleLabels[role].title}</strong><small>{roleLabels[role].detail}</small></button>)}
              </div>
            </section>
            <section className="custom-section">
              <div className="guided-checklist">
                <div><span>1</span><strong>Choose your role</strong><small>Apply a complete, reversible starting profile.</small></div>
                <div><span>2</span><strong>Refine your workspace</strong><small>Adjust tabs, dashboard, display, queues, and actions.</small></div>
                <div><span>3</span><strong>Save or transfer</strong><small>Create a named profile or share a portable package.</small></div>
              </div>
              <button type="button" className="secondary-button" onClick={() => { update({ ...preferences, onboarding: { complete: true, role: null, completedAt: new Date().toISOString() } }, 'Guided setup dismissed.'); setTab('customize'); }}>Keep current setup</button>
            </section>
          </> : null}

          {tab === 'customize' ? <>
            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">1</span><h3>Mobile navigation</h3></div><span>{preferences.mobileTabs.length}/4 selected</span></div>
              <p>Choose up to four destinations that remain visible in the phone bottom bar.</p>
              <div className="custom-choice-grid">
                {navigationOptions.map((item) => {
                  const selected = preferences.mobileTabs.includes(item.id);
                  return <button key={item.id} type="button" className={`custom-choice${selected ? ' is-selected' : ''}`} onClick={() => toggleMobileTab(item.id)} disabled={!selected && preferences.mobileTabs.length >= 4}><span>{item.symbol}</span><strong>{item.label}</strong><small>{item.description}</small></button>;
                })}
              </div>
              <div className="ordered-list">
                {preferences.mobileTabs.map((id, index) => {
                  const item = navigationOptions.find((option) => option.id === id);
                  return <div key={id} className="ordered-row"><span className="drag-index">{index + 1}</span><strong>{item?.label ?? id}</strong><div><button type="button" className="icon-button small-icon-button" aria-label={`Move ${item?.label ?? id} earlier`} onClick={() => update({ ...preferences, activeProfileId: null, mobileTabs: moveItem(preferences.mobileTabs, index, -1) })} disabled={index === 0}>↑</button><button type="button" className="icon-button small-icon-button" aria-label={`Move ${item?.label ?? id} later`} onClick={() => update({ ...preferences, activeProfileId: null, mobileTabs: moveItem(preferences.mobileTabs, index, 1) })} disabled={index === preferences.mobileTabs.length - 1}>↓</button></div></div>;
                })}
              </div>
            </section>

            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">2</span><h3>Home dashboard</h3></div><span>Show, hide, and reorder</span></div>
              <div className="ordered-list">
                {preferences.dashboard.order.map((id, index) => {
                  const visible = !preferences.dashboard.hidden.includes(id);
                  return <div key={id} className={`ordered-row dashboard-order-row${visible ? '' : ' is-muted'}`}><label><input type="checkbox" checked={visible} onChange={() => toggleWidget(id)} /><strong>{dashboardLabels[id]}</strong></label><div><button type="button" className="icon-button small-icon-button" onClick={() => update({ ...preferences, activeProfileId: null, dashboard: { ...preferences.dashboard, order: moveItem(preferences.dashboard.order, index, -1) } })} disabled={index === 0}>↑</button><button type="button" className="icon-button small-icon-button" onClick={() => update({ ...preferences, activeProfileId: null, dashboard: { ...preferences.dashboard, order: moveItem(preferences.dashboard.order, index, 1) } })} disabled={index === preferences.dashboard.order.length - 1}>↓</button></div></div>;
                })}
              </div>
            </section>

            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">3</span><h3>Display and accessibility</h3></div><span>Device-specific</span></div>
              <div className="profile-grid">
                {(Object.keys(profileLabels) as DisplayProfileId[]).map((profileId) => <button key={profileId} type="button" className={`profile-option${preferences.display.profile === profileId ? ' is-selected' : ''}`} onClick={() => update(applyDisplayProfile(preferences, profileId))}><strong>{profileLabels[profileId].title}</strong><small>{profileLabels[profileId].detail}</small></button>)}
              </div>
              <div className="custom-form-grid">
                <label>Appearance<select value={preferences.display.theme} onChange={(event: ChangeEvent<HTMLSelectElement>) => update({ ...preferences, activeProfileId: null, display: { ...preferences.display, theme: event.target.value as UserPreferences['display']['theme'] } })}><option value="system">Follow device</option><option value="light">Light</option><option value="dark">Dark</option></select></label>
                <label>Text size<select value={preferences.display.textScale} onChange={(event: ChangeEvent<HTMLSelectElement>) => update({ ...preferences, activeProfileId: null, display: { ...preferences.display, textScale: Number(event.target.value) } })}><option value={0.96}>Small</option><option value={1}>Standard</option><option value={1.08}>Large</option><option value={1.16}>Extra large</option></select></label>
              </div>
              <fieldset className="accent-fieldset"><legend>Accent</legend><div className="accent-options">{(Object.keys(ACCENT_COLORS) as Array<keyof typeof ACCENT_COLORS>).map((accent) => <button key={accent} type="button" className={`accent-option${preferences.display.accent === accent ? ' is-selected' : ''}`} style={{ '--swatch': ACCENT_COLORS[accent].base } as CSSProperties} onClick={() => update({ ...preferences, activeProfileId: null, display: { ...preferences.display, accent } })}><span /><small>{accent}</small></button>)}</div></fieldset>
              <div className="toggle-list">
                <label><input type="checkbox" checked={preferences.display.highContrast} onChange={(event: ChangeEvent<HTMLInputElement>) => update({ ...preferences, activeProfileId: null, display: { ...preferences.display, highContrast: event.target.checked } })} /><span><strong>High contrast</strong><small>Strengthen borders and status separation.</small></span></label>
                <label><input type="checkbox" checked={preferences.display.reducedMotion} onChange={(event: ChangeEvent<HTMLInputElement>) => update({ ...preferences, activeProfileId: null, display: { ...preferences.display, reducedMotion: event.target.checked } })} /><span><strong>Reduced motion</strong><small>Disable nonessential transitions and smooth scrolling.</small></span></label>
                <label><input type="checkbox" checked={preferences.display.leftHanded} onChange={(event: ChangeEvent<HTMLInputElement>) => update({ ...preferences, activeProfileId: null, display: { ...preferences.display, leftHanded: event.target.checked } })} /><span><strong>Left-handed mobile controls</strong><small>Move floating controls to the left edge.</small></span></label>
              </div>
            </section>

            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">4</span><h3>Queues and alerts</h3></div><span>Personal prioritization</span></div>
              <div className="custom-form-grid">
                <label>Start workspace<select value={preferences.queue.startView} onChange={(event: ChangeEvent<HTMLSelectElement>) => update({ ...preferences, activeProfileId: null, queue: { ...preferences.queue, startView: event.target.value as ViewId } })}>{navigationOptions.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label>
                <label>Default findings<select value={preferences.queue.findingSeverity} onChange={(event: ChangeEvent<HTMLSelectElement>) => update({ ...preferences, activeProfileId: null, queue: { ...preferences.queue, findingSeverity: event.target.value as UserPreferences['queue']['findingSeverity'] } })}><option>All</option><option>Error</option><option>Warning</option><option>Info</option></select></label>
                <label>Queue order<select value={preferences.queue.sort} onChange={(event: ChangeEvent<HTMLSelectElement>) => update({ ...preferences, activeProfileId: null, queue: { ...preferences.queue, sort: event.target.value as UserPreferences['queue']['sort'] } })}><option value="severity">Severity first</option><option value="newest">Newest first</option></select></label>
                <label>Alert delivery<select value={preferences.alerts.mode} onChange={(event: ChangeEvent<HTMLSelectElement>) => update({ ...preferences, activeProfileId: null, alerts: { ...preferences.alerts, mode: event.target.value as UserPreferences['alerts']['mode'] } })}><option value="immediate">Immediate</option><option value="digest">Daily digest</option><option value="muted">Muted</option></select></label>
              </div>
              <div className="toggle-list">
                <label><input type="checkbox" checked={preferences.queue.hideAccepted} onChange={(event: ChangeEvent<HTMLInputElement>) => update({ ...preferences, activeProfileId: null, queue: { ...preferences.queue, hideAccepted: event.target.checked } })} /><span><strong>Hide accepted evidence</strong><small>Remove completed artifacts from the personal review queue.</small></span></label>
                <label><input type="checkbox" checked={preferences.alerts.quietHours} onChange={(event: ChangeEvent<HTMLInputElement>) => update({ ...preferences, activeProfileId: null, alerts: { ...preferences.alerts, quietHours: event.target.checked } })} /><span><strong>Quiet hours</strong><small>Suppress noncritical alerts during the device quiet period.</small></span></label>
              </div>
              <div className="toggle-list alert-toggle-list">
                {(Object.keys(alertLabels) as AlertRuleId[]).map((id) => <label key={id}><input type="checkbox" checked={preferences.alerts.enabled[id]} onChange={(event: ChangeEvent<HTMLInputElement>) => update({ ...preferences, activeProfileId: null, alerts: { ...preferences.alerts, enabled: { ...preferences.alerts.enabled, [id]: event.target.checked } } })} /><span><strong>{alertLabels[id]}</strong></span></label>)}
              </div>
            </section>

            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">5</span><h3>Quick actions</h3></div><span>{preferences.quickActions.length}/4 selected</span></div>
              <div className="quick-action-options">
                {(Object.keys(quickActionLabels) as QuickActionId[]).map((id) => <label key={id} className={preferences.quickActions.includes(id) ? 'is-selected' : ''}><input type="checkbox" checked={preferences.quickActions.includes(id)} onChange={() => toggleQuickAction(id)} disabled={!preferences.quickActions.includes(id) && preferences.quickActions.length >= 4} /><span>{quickActionLabels[id]}</span></label>)}
              </div>
            </section>
          </> : null}

          {tab === 'profiles' ? <>
            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">A</span><h3>Named workspace profiles</h3></div><span>{preferences.profiles.length} available</span></div>
              <p>Profiles capture presentation and personal workflow only. Switching profiles never changes evidence or governed decisions.</p>
              <div className="workspace-profile-grid">
                {preferences.profiles.map((item) => <article key={item.id} className={`workspace-profile-card${preferences.activeProfileId === item.id ? ' is-active' : ''}`}><header><div><strong>{item.name}</strong><small>{item.builtIn ? 'Built-in profile' : 'Custom profile'}</small></div>{preferences.activeProfileId === item.id ? <span className="profile-active-badge">Active</span> : null}</header><p>{item.description || 'Saved personal workspace configuration.'}</p><footer><button type="button" className="secondary-button" onClick={() => update(applyWorkspaceProfile(preferences, item.id), `Applied “${item.name}”.`)}>Apply</button>{!item.builtIn ? <><button type="button" className="secondary-button" onClick={() => update(updateWorkspaceProfile(preferences, item.id), `Updated “${item.name}” from the current workspace.`)}>Update</button><button type="button" className="danger-text-button" onClick={() => update(deleteWorkspaceProfile(preferences, item.id), `Deleted “${item.name}”.`)}>Delete</button></> : null}</footer></article>)}
              </div>
            </section>
            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">B</span><h3>Save current workspace</h3></div><span>Custom profile</span></div>
              <div className="profile-create-grid"><label>Profile name<input value={profileName} maxLength={60} placeholder="My review mode" onChange={(event: ChangeEvent<HTMLInputElement>) => setProfileName(event.target.value)} /></label><label>Description<input value={profileDescription} maxLength={180} placeholder="When and why I use this layout" onChange={(event: ChangeEvent<HTMLInputElement>) => setProfileDescription(event.target.value)} /></label><button type="button" className="primary-button" onClick={createProfile}>Save profile</button></div>
            </section>
          </> : null}

          {tab === 'transfer' ? <>
            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">A</span><h3>Portable preference package</h3></div><span>Evidence excluded</span></div>
              <p>Transfer your workspace between phones, tablets, and desktops using a checksum-protected JSON package. No evidence, findings, approvals, policies, or audit records are included.</p>
              <label className="device-label-field">Device label<input value={deviceLabel} maxLength={80} onChange={(event: ChangeEvent<HTMLInputElement>) => setDeviceLabel(event.target.value)} /></label>
              <div className="portable-category-grid">
                {PREFERENCE_CATEGORIES.map((category) => <label key={category} className={exportCategories.includes(category) ? 'is-selected' : ''}><input type="checkbox" checked={exportCategories.includes(category)} onChange={() => toggleCategory(category, exportCategories, setExportCategories)} /><span><strong>{categoryLabels[category].title}</strong><small>{categoryLabels[category].detail}</small></span></label>)}
              </div>
              <div className="portable-action-row"><button type="button" className="primary-button" onClick={() => void exportPreferences(true)}>Share package</button><button type="button" className="secondary-button" onClick={() => void exportPreferences(false)}>Download JSON</button><button type="button" className="secondary-button" onClick={() => void copyPreferences()}>Copy JSON</button></div>
            </section>
            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">B</span><h3>Import with preview</h3></div><span>Selective apply</span></div>
              <input ref={fileInputRef} type="file" accept="application/json,.json" hidden onChange={(event: ChangeEvent<HTMLInputElement>) => void readImport(event)} />
              <button type="button" className="secondary-button" onClick={() => fileInputRef.current?.click()}>Choose preference package</button>
              {importPackage ? <div className="import-preview"><header><div><strong>{importPackage.deviceLabel}</strong><small>Exported {new Date(importPackage.exportedAt).toLocaleString()} · checksum verified</small></div><span className="profile-active-badge">Valid</span></header><div className="portable-category-grid">{importPackage.categories.map((category) => <label key={category} className={importCategories.includes(category) ? 'is-selected' : ''}><input type="checkbox" checked={importCategories.includes(category)} onChange={() => toggleCategory(category, importCategories, setImportCategories)} /><span><strong>{categoryLabels[category].title}</strong><small>{categoryLabels[category].detail}</small></span></label>)}</div><div className="portable-action-row"><button type="button" className="primary-button" disabled={importCategories.length === 0} onClick={applyImport}>Apply selected categories</button><button type="button" className="secondary-button" onClick={() => { setImportPackage(null); setImportCategories([]); }}>Cancel</button></div></div> : null}
            </section>
            <section className="custom-section sync-boundary-card"><span className="eyebrow">Continuous synchronization</span><h3>Provider-ready, intentionally disabled</h3><p>Automatic cloud sync is not enabled because Workbench Studio does not yet have an authenticated preference-storage provider. Portable sharing is available now; evidence never enters the transfer package.</p><span className="status-chip">No false sync state</span></section>
          </> : null}

          {tab === 'recovery' ? <>
            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">A</span><h3>Preference diagnostics</h3></div><span>Local browser state</span></div>
              <div className="diagnostic-grid">
                <div><span>Schema</span><strong>v{diagnostics.schemaVersion}</strong></div><div><span>Stored size</span><strong>{diagnostics.storageBytes.toLocaleString()} bytes</strong></div><div><span>Profiles</span><strong>{diagnostics.profileCount}</strong></div><div><span>Backup</span><strong>{diagnostics.backupAvailable ? 'Available' : 'None'}</strong></div><div><span>Last saved</span><strong>{diagnostics.lastSavedAt ? new Date(diagnostics.lastSavedAt).toLocaleString() : 'Not recorded'}</strong></div><div><span>Last import</span><strong>{diagnostics.lastImportAt ? new Date(diagnostics.lastImportAt).toLocaleString() : 'Never'}</strong></div>
              </div>
              {diagnostics.lastError ? <div className="diagnostic-error"><strong>Last recovery note</strong><span>{diagnostics.lastError}</span></div> : null}
              <div className="portable-action-row"><button type="button" className="secondary-button" disabled={!diagnostics.backupAvailable} onClick={restoreBackup}>Restore last known good</button><button type="button" className="secondary-button" onClick={() => downloadText('workbench-preference-diagnostics.json', serializePreferenceDiagnostics(preferences))}>Download diagnostics</button><button type="button" className="secondary-button" onClick={() => { update(restartOnboarding(preferences), 'Guided setup restarted.'); setTab('guide'); }}>Re-run guided setup</button></div>
            </section>
            <section className="custom-section">
              <div className="custom-section-heading"><div><span className="custom-step">B</span><h3>Reset one category</h3></div><span>Keep everything else</span></div>
              <div className="category-reset-grid">{PREFERENCE_CATEGORIES.map((category) => <button key={category} type="button" onClick={() => update(resetPreferenceCategory(preferences, category), `Reset ${categoryLabels[category].title.toLowerCase()} to recommended defaults.`)}><strong>{categoryLabels[category].title}</strong><small>{categoryLabels[category].detail}</small></button>)}</div>
            </section>
          </> : null}
        </div>

        <footer className="customization-footer"><button type="button" className="danger-text-button" onClick={onReset}>Reset all preferences</button><span>Schema v2 · Workbench Studio 0.8.4</span><button type="button" className="primary-button" onClick={onClose}>Done</button></footer>
      </section>
    </div>
  );
}
