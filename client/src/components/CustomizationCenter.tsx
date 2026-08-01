import type { CSSProperties, ChangeEvent, MouseEvent as ReactMouseEvent } from 'react';
import type { ViewId } from '../lib/types';
import {
  ACCENT_COLORS,
  applyDisplayProfile,
  type AlertRuleId,
  type DashboardWidgetId,
  type DisplayProfileId,
  type QuickActionId,
  type UserPreferences,
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

export function CustomizationCenter({ open, preferences, navigationOptions, onChange, onClose, onReset }: CustomizationCenterProps) {
  if (!open) return null;

  function toggleMobileTab(id: ViewId) {
    const selected = preferences.mobileTabs.includes(id);
    if (selected && preferences.mobileTabs.length <= 4) {
      onChange({ ...preferences, mobileTabs: preferences.mobileTabs.filter((item) => item !== id) });
      return;
    }
    if (!selected && preferences.mobileTabs.length >= 4) return;
    onChange({ ...preferences, mobileTabs: selected ? preferences.mobileTabs.filter((item) => item !== id) : [...preferences.mobileTabs, id] });
  }

  function toggleWidget(id: DashboardWidgetId) {
    const hidden = preferences.dashboard.hidden.includes(id);
    onChange({
      ...preferences,
      dashboard: {
        ...preferences.dashboard,
        hidden: hidden ? preferences.dashboard.hidden.filter((item) => item !== id) : [...preferences.dashboard.hidden, id],
      },
    });
  }

  function toggleQuickAction(id: QuickActionId) {
    const selected = preferences.quickActions.includes(id);
    if (!selected && preferences.quickActions.length >= 4) return;
    onChange({
      ...preferences,
      quickActions: selected ? preferences.quickActions.filter((item) => item !== id) : [...preferences.quickActions, id],
    });
  }

  return (
    <div className="customization-backdrop" role="presentation" onMouseDown={(event: ReactMouseEvent<HTMLDivElement>) => { if (event.target === event.currentTarget) onClose(); }}>
      <section className="customization-panel" role="dialog" aria-modal="true" aria-labelledby="customization-heading">
        <header className="customization-header">
          <div><span className="eyebrow">Personal workspace</span><h2 id="customization-heading">Customize Workbench Studio</h2><p>Preferences change presentation and shortcuts only. Evidence, policy results, findings, and approvals remain authoritative.</p></div>
          <button type="button" className="icon-button" aria-label="Close customization" onClick={onClose}>×</button>
        </header>

        <div className="customization-content">
          <section className="custom-section">
            <div className="custom-section-heading"><div><span className="custom-step">1</span><h3>Mobile navigation</h3></div><span>{preferences.mobileTabs.length}/4 selected</span></div>
            <p>Choose the four destinations that remain visible in the phone bottom bar.</p>
            <div className="custom-choice-grid">
              {navigationOptions.map((item) => {
                const selected = preferences.mobileTabs.includes(item.id);
                return <button key={item.id} type="button" className={`custom-choice${selected ? ' is-selected' : ''}`} onClick={() => toggleMobileTab(item.id)} disabled={!selected && preferences.mobileTabs.length >= 4}><span>{item.symbol}</span><strong>{item.label}</strong><small>{item.description}</small></button>;
              })}
            </div>
            <div className="ordered-list">
              {preferences.mobileTabs.map((id, index) => {
                const item = navigationOptions.find((option) => option.id === id);
                return <div key={id} className="ordered-row"><span className="drag-index">{index + 1}</span><strong>{item?.label ?? id}</strong><div><button type="button" className="icon-button small-icon-button" aria-label={`Move ${item?.label ?? id} earlier`} onClick={() => onChange({ ...preferences, mobileTabs: moveItem(preferences.mobileTabs, index, -1) })} disabled={index === 0}>↑</button><button type="button" className="icon-button small-icon-button" aria-label={`Move ${item?.label ?? id} later`} onClick={() => onChange({ ...preferences, mobileTabs: moveItem(preferences.mobileTabs, index, 1) })} disabled={index === preferences.mobileTabs.length - 1}>↓</button></div></div>;
              })}
            </div>
          </section>

          <section className="custom-section">
            <div className="custom-section-heading"><div><span className="custom-step">2</span><h3>Home dashboard</h3></div><span>Show, hide, and reorder</span></div>
            <p>Arrange the overview around the work you perform most often.</p>
            <div className="ordered-list">
              {preferences.dashboard.order.map((id, index) => {
                const visible = !preferences.dashboard.hidden.includes(id);
                return <div key={id} className={`ordered-row dashboard-order-row${visible ? '' : ' is-muted'}`}><label><input type="checkbox" checked={visible} onChange={() => toggleWidget(id)} /><strong>{dashboardLabels[id]}</strong></label><div><button type="button" className="icon-button small-icon-button" onClick={() => onChange({ ...preferences, dashboard: { ...preferences.dashboard, order: moveItem(preferences.dashboard.order, index, -1) } })} disabled={index === 0}>↑</button><button type="button" className="icon-button small-icon-button" onClick={() => onChange({ ...preferences, dashboard: { ...preferences.dashboard, order: moveItem(preferences.dashboard.order, index, 1) } })} disabled={index === preferences.dashboard.order.length - 1}>↓</button></div></div>;
              })}
            </div>
          </section>

          <section className="custom-section">
            <div className="custom-section-heading"><div><span className="custom-step">3</span><h3>Display and accessibility</h3></div><span>Device-specific</span></div>
            <div className="profile-grid">
              {(Object.keys(profileLabels) as DisplayProfileId[]).map((profile) => <button key={profile} type="button" className={`profile-option${preferences.display.profile === profile ? ' is-selected' : ''}`} onClick={() => onChange(applyDisplayProfile(preferences, profile))}><strong>{profileLabels[profile].title}</strong><small>{profileLabels[profile].detail}</small></button>)}
            </div>
            <div className="custom-form-grid">
              <label>Appearance<select value={preferences.display.theme} onChange={(event: ChangeEvent<HTMLSelectElement>) => onChange({ ...preferences, display: { ...preferences.display, theme: event.target.value as UserPreferences['display']['theme'] } })}><option value="system">Follow device</option><option value="light">Light</option><option value="dark">Dark</option></select></label>
              <label>Text size<select value={preferences.display.textScale} onChange={(event: ChangeEvent<HTMLSelectElement>) => onChange({ ...preferences, display: { ...preferences.display, textScale: Number(event.target.value) } })}><option value={0.96}>Small</option><option value={1}>Standard</option><option value={1.08}>Large</option><option value={1.16}>Extra large</option></select></label>
            </div>
            <fieldset className="accent-fieldset"><legend>Accent</legend><div className="accent-options">{(Object.keys(ACCENT_COLORS) as Array<keyof typeof ACCENT_COLORS>).map((accent) => <button key={accent} type="button" className={`accent-option${preferences.display.accent === accent ? ' is-selected' : ''}`} style={{ '--swatch': ACCENT_COLORS[accent].base } as CSSProperties} onClick={() => onChange({ ...preferences, display: { ...preferences.display, accent } })}><span /><small>{accent}</small></button>)}</div></fieldset>
            <div className="toggle-list">
              <label><input type="checkbox" checked={preferences.display.highContrast} onChange={(event: ChangeEvent<HTMLInputElement>) => onChange({ ...preferences, display: { ...preferences.display, highContrast: event.target.checked } })} /><span><strong>High contrast</strong><small>Strengthen borders and status separation.</small></span></label>
              <label><input type="checkbox" checked={preferences.display.reducedMotion} onChange={(event: ChangeEvent<HTMLInputElement>) => onChange({ ...preferences, display: { ...preferences.display, reducedMotion: event.target.checked } })} /><span><strong>Reduced motion</strong><small>Disable nonessential transitions and smooth scrolling.</small></span></label>
              <label><input type="checkbox" checked={preferences.display.leftHanded} onChange={(event: ChangeEvent<HTMLInputElement>) => onChange({ ...preferences, display: { ...preferences.display, leftHanded: event.target.checked } })} /><span><strong>Left-handed mobile controls</strong><small>Move floating controls to the left edge.</small></span></label>
            </div>
          </section>

          <section className="custom-section">
            <div className="custom-section-heading"><div><span className="custom-step">4</span><h3>Queues and alerts</h3></div><span>Personal prioritization</span></div>
            <div className="custom-form-grid">
              <label>Start workspace<select value={preferences.queue.startView} onChange={(event: ChangeEvent<HTMLSelectElement>) => onChange({ ...preferences, queue: { ...preferences.queue, startView: event.target.value as ViewId } })}>{navigationOptions.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label>
              <label>Default findings<select value={preferences.queue.findingSeverity} onChange={(event: ChangeEvent<HTMLSelectElement>) => onChange({ ...preferences, queue: { ...preferences.queue, findingSeverity: event.target.value as UserPreferences['queue']['findingSeverity'] } })}><option>All</option><option>Error</option><option>Warning</option><option>Info</option></select></label>
              <label>Queue order<select value={preferences.queue.sort} onChange={(event: ChangeEvent<HTMLSelectElement>) => onChange({ ...preferences, queue: { ...preferences.queue, sort: event.target.value as UserPreferences['queue']['sort'] } })}><option value="severity">Severity first</option><option value="newest">Newest first</option></select></label>
              <label>Alert delivery<select value={preferences.alerts.mode} onChange={(event: ChangeEvent<HTMLSelectElement>) => onChange({ ...preferences, alerts: { ...preferences.alerts, mode: event.target.value as UserPreferences['alerts']['mode'] } })}><option value="immediate">Immediate</option><option value="digest">Daily digest</option><option value="muted">Muted</option></select></label>
            </div>
            <div className="toggle-list">
              <label><input type="checkbox" checked={preferences.queue.hideAccepted} onChange={(event: ChangeEvent<HTMLInputElement>) => onChange({ ...preferences, queue: { ...preferences.queue, hideAccepted: event.target.checked } })} /><span><strong>Hide accepted evidence</strong><small>Remove completed artifacts from the personal review queue.</small></span></label>
              <label><input type="checkbox" checked={preferences.alerts.quietHours} onChange={(event: ChangeEvent<HTMLInputElement>) => onChange({ ...preferences, alerts: { ...preferences.alerts, quietHours: event.target.checked } })} /><span><strong>Quiet hours</strong><small>Suppress noncritical alerts during the device quiet period.</small></span></label>
            </div>
            <div className="toggle-list alert-toggle-list">
              {(Object.keys(alertLabels) as AlertRuleId[]).map((id) => <label key={id}><input type="checkbox" checked={preferences.alerts.enabled[id]} onChange={(event: ChangeEvent<HTMLInputElement>) => onChange({ ...preferences, alerts: { ...preferences.alerts, enabled: { ...preferences.alerts.enabled, [id]: event.target.checked } } })} /><span><strong>{alertLabels[id]}</strong></span></label>)}
            </div>
          </section>

          <section className="custom-section">
            <div className="custom-section-heading"><div><span className="custom-step">5</span><h3>Quick actions</h3></div><span>{preferences.quickActions.length}/4 selected</span></div>
            <p>Selected actions appear above the mobile bottom navigation. Approval, destructive, and privacy-sensitive actions are intentionally excluded.</p>
            <div className="quick-action-options">
              {(Object.keys(quickActionLabels) as QuickActionId[]).map((id) => <label key={id} className={preferences.quickActions.includes(id) ? 'is-selected' : ''}><input type="checkbox" checked={preferences.quickActions.includes(id)} onChange={() => toggleQuickAction(id)} disabled={!preferences.quickActions.includes(id) && preferences.quickActions.length >= 4} /><span>{quickActionLabels[id]}</span></label>)}
            </div>
          </section>
        </div>

        <footer className="customization-footer"><button type="button" className="danger-text-button" onClick={onReset}>Reset all preferences</button><button type="button" className="primary-button" onClick={onClose}>Done</button></footer>
      </section>
    </div>
  );
}
