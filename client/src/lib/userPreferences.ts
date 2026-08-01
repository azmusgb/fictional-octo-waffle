import type { ViewId } from './types';

export type AccentId = 'blue' | 'purple' | 'teal' | 'gold' | 'rose';
export type DisplayProfileId = 'standard' | 'large-touch' | 'high-contrast' | 'dense-review' | 'evidence-reading';
export type DashboardWidgetId = 'metrics' | 'processing' | 'intake' | 'insights';
export type QuickActionId = 'review' | 'findings' | 'compare' | 'command' | 'operations' | 'exports' | 'customize';
export type AlertRuleId = 'criticalFinding' | 'approvalAssigned' | 'privacyDetected' | 'baselineRegression' | 'automationFailed' | 'agentDisconnected';
export type WorkspaceRoleId = 'investigator' | 'reviewer' | 'approver' | 'operations' | 'executive';
export type PreferenceCategory = 'navigation' | 'dashboard' | 'display' | 'queue' | 'alerts' | 'quickActions' | 'profiles';

export interface PreferenceSnapshot {
  mobileTabs: ViewId[];
  dashboard: {
    order: DashboardWidgetId[];
    hidden: DashboardWidgetId[];
  };
  display: {
    profile: DisplayProfileId;
    theme: 'system' | 'light' | 'dark';
    density: 'comfortable' | 'compact';
    textScale: number;
    accent: AccentId;
    reducedMotion: boolean;
    highContrast: boolean;
    leftHanded: boolean;
  };
  queue: {
    startView: ViewId;
    findingSeverity: 'All' | 'Error' | 'Warning' | 'Info';
    sort: 'severity' | 'newest';
    hideAccepted: boolean;
  };
  alerts: {
    mode: 'immediate' | 'digest' | 'muted';
    quietHours: boolean;
    enabled: Record<AlertRuleId, boolean>;
  };
  quickActions: QuickActionId[];
}

export interface WorkspaceProfile {
  id: string;
  name: string;
  description: string;
  builtIn: boolean;
  createdAt: string;
  updatedAt: string;
  snapshot: PreferenceSnapshot;
}

export interface UserPreferences extends PreferenceSnapshot {
  version: 2;
  activeProfileId: string | null;
  profiles: WorkspaceProfile[];
  onboarding: {
    complete: boolean;
    role: WorkspaceRoleId | null;
    completedAt: string | null;
  };
  portability: {
    lastExportAt: string | null;
    lastImportAt: string | null;
    lastImportSource: string | null;
  };
}

export interface PreferencesPackage {
  format: 'workbench-studio-preferences';
  packageVersion: 1;
  schemaVersion: 2;
  appVersion: string;
  exportedAt: string;
  deviceLabel: string;
  categories: PreferenceCategory[];
  preferences: Partial<PreferenceSnapshot>;
  profiles: WorkspaceProfile[];
  checksum: string;
}

export interface PreferenceDiagnostics {
  schemaVersion: 2;
  storageKey: string;
  storageBytes: number;
  lastSavedAt: string | null;
  backupAvailable: boolean;
  backupSavedAt: string | null;
  profileCount: number;
  onboardingComplete: boolean;
  activeProfileId: string | null;
  lastExportAt: string | null;
  lastImportAt: string | null;
  lastImportSource: string | null;
  lastError: string | null;
}

interface PreferenceMeta {
  lastSavedAt: string | null;
  backupSavedAt: string | null;
  lastError: string | null;
}

export const ACCENT_COLORS: Record<AccentId, { base: string; strong: string; soft: string }> = {
  blue: { base: '#5B8CFF', strong: '#3566D7', soft: '#EAF0FF' },
  purple: { base: '#7C5CFC', strong: '#5A3ED8', soft: '#F0ECFF' },
  teal: { base: '#21B89A', strong: '#087E6B', soft: '#E5F8F4' },
  gold: { base: '#F2A93B', strong: '#A96500', soft: '#FFF4DF' },
  rose: { base: '#E45B73', strong: '#B62D4A', soft: '#FDEBF0' },
};

export const DISPLAY_PROFILES: Record<DisplayProfileId, Pick<PreferenceSnapshot['display'], 'density' | 'textScale' | 'reducedMotion' | 'highContrast'>> = {
  standard: { density: 'comfortable', textScale: 1, reducedMotion: false, highContrast: false },
  'large-touch': { density: 'comfortable', textScale: 1.08, reducedMotion: false, highContrast: false },
  'high-contrast': { density: 'comfortable', textScale: 1.04, reducedMotion: true, highContrast: true },
  'dense-review': { density: 'compact', textScale: 0.96, reducedMotion: false, highContrast: false },
  'evidence-reading': { density: 'comfortable', textScale: 1.12, reducedMotion: true, highContrast: false },
};

const VALID_VIEW_IDS: ViewId[] = ['overview', 'inventory', 'review', 'findings', 'compare', 'operations', 'decisions', 'command', 'exports', 'system'];
const DASHBOARD_WIDGETS: DashboardWidgetId[] = ['metrics', 'processing', 'intake', 'insights'];
const QUICK_ACTIONS: QuickActionId[] = ['review', 'findings', 'compare', 'command', 'operations', 'exports', 'customize'];
const ALERT_RULES: AlertRuleId[] = ['criticalFinding', 'approvalAssigned', 'privacyDetected', 'baselineRegression', 'automationFailed', 'agentDisconnected'];
export const PREFERENCE_CATEGORIES: PreferenceCategory[] = ['navigation', 'dashboard', 'display', 'queue', 'alerts', 'quickActions', 'profiles'];

const STORAGE_KEY = 'ws.userPreferences.v2';
const LEGACY_STORAGE_KEY = 'ws.userPreferences.v1';
const BACKUP_KEY = 'ws.userPreferences.v2.backup';
const META_KEY = 'ws.userPreferences.v2.meta';
const BUILT_IN_DATE = '2026-08-01T00:00:00.000Z';

const BASE_SNAPSHOT: PreferenceSnapshot = {
  mobileTabs: ['overview', 'inventory', 'review', 'command'],
  dashboard: {
    order: ['metrics', 'processing', 'intake', 'insights'],
    hidden: [],
  },
  display: {
    profile: 'standard',
    theme: 'system',
    density: 'comfortable',
    textScale: 1,
    accent: 'blue',
    reducedMotion: false,
    highContrast: false,
    leftHanded: false,
  },
  queue: {
    startView: 'overview',
    findingSeverity: 'All',
    sort: 'severity',
    hideAccepted: false,
  },
  alerts: {
    mode: 'immediate',
    quietHours: true,
    enabled: {
      criticalFinding: true,
      approvalAssigned: true,
      privacyDetected: true,
      baselineRegression: true,
      automationFailed: true,
      agentDisconnected: false,
    },
  },
  quickActions: ['review', 'findings', 'command'],
};

function profile(id: string, name: string, description: string, snapshot: Partial<PreferenceSnapshot>): WorkspaceProfile {
  return {
    id,
    name,
    description,
    builtIn: true,
    createdAt: BUILT_IN_DATE,
    updatedAt: BUILT_IN_DATE,
    snapshot: normalizeSnapshot({ ...BASE_SNAPSHOT, ...snapshot }),
  };
}

export const BUILT_IN_PROFILES: WorkspaceProfile[] = [
  profile('profile-daily-review', 'Daily Review', 'Review-first layout for high-volume evidence decisions.', {
    mobileTabs: ['overview', 'review', 'findings', 'command'],
    queue: { ...BASE_SNAPSHOT.queue, startView: 'review', sort: 'severity', hideAccepted: true },
    quickActions: ['review', 'findings', 'command', 'compare'],
  }),
  profile('profile-evidence-reading', 'Evidence Reading', 'Larger source text with reduced motion and direct evidence access.', {
    mobileTabs: ['overview', 'inventory', 'findings', 'compare'],
    display: { ...BASE_SNAPSHOT.display, ...DISPLAY_PROFILES['evidence-reading'], profile: 'evidence-reading' },
    queue: { ...BASE_SNAPSHOT.queue, startView: 'inventory' },
    quickActions: ['findings', 'compare', 'command'],
  }),
  profile('profile-approval-mode', 'Approval Mode', 'Decision, approval, and executive readiness controls.', {
    mobileTabs: ['overview', 'decisions', 'command', 'exports'],
    queue: { ...BASE_SNAPSHOT.queue, startView: 'decisions', hideAccepted: true },
    alerts: { ...BASE_SNAPSHOT.alerts, enabled: { ...BASE_SNAPSHOT.alerts.enabled, approvalAssigned: true, baselineRegression: true } },
    quickActions: ['command', 'compare', 'exports', 'findings'],
  }),
  profile('profile-executive-check-in', 'Executive Check-in', 'Compact readiness, risks, and decision delivery.', {
    mobileTabs: ['overview', 'command', 'decisions', 'exports'],
    dashboard: { order: ['metrics', 'insights', 'processing', 'intake'], hidden: ['processing'] },
    queue: { ...BASE_SNAPSHOT.queue, startView: 'command' },
    alerts: { ...BASE_SNAPSHOT.alerts, mode: 'digest' },
    quickActions: ['command', 'exports', 'compare'],
  }),
  profile('profile-mobile-field-work', 'Mobile Field Work', 'Large-touch controls and operations-first navigation.', {
    mobileTabs: ['overview', 'operations', 'inventory', 'review'],
    display: { ...BASE_SNAPSHOT.display, ...DISPLAY_PROFILES['large-touch'], profile: 'large-touch' },
    queue: { ...BASE_SNAPSHOT.queue, startView: 'operations' },
    quickActions: ['operations', 'review', 'findings', 'customize'],
  }),
];

export const DEFAULT_USER_PREFERENCES: UserPreferences = {
  version: 2,
  ...structuredClone(BASE_SNAPSHOT),
  activeProfileId: 'profile-daily-review',
  profiles: structuredClone(BUILT_IN_PROFILES),
  onboarding: {
    complete: false,
    role: null,
    completedAt: null,
  },
  portability: {
    lastExportAt: null,
    lastImportAt: null,
    lastImportSource: null,
  },
};

const ROLE_PROFILE_MAP: Record<WorkspaceRoleId, string> = {
  investigator: 'profile-evidence-reading',
  reviewer: 'profile-daily-review',
  approver: 'profile-approval-mode',
  operations: 'profile-mobile-field-work',
  executive: 'profile-executive-check-in',
};

function clone<T>(value: T): T {
  return structuredClone(value);
}

function isViewId(value: unknown): value is ViewId {
  return typeof value === 'string' && VALID_VIEW_IDS.includes(value as ViewId);
}

function uniqueAllowed<T extends string>(values: unknown, allowed: readonly T[], maximum?: number): T[] {
  if (!Array.isArray(values)) return [];
  const result = [...new Set(values.filter((item): item is T => typeof item === 'string' && allowed.includes(item as T)))];
  return typeof maximum === 'number' ? result.slice(0, maximum) : result;
}

function normalizeSnapshot(value: Partial<PreferenceSnapshot> | undefined): PreferenceSnapshot {
  const source = value ?? {};
  const mobileTabs = uniqueAllowed(source.mobileTabs, VALID_VIEW_IDS, 4);
  const order = uniqueAllowed(source.dashboard?.order, DASHBOARD_WIDGETS);
  const normalizedOrder = [...order, ...DASHBOARD_WIDGETS.filter((item) => !order.includes(item))];
  const hidden = uniqueAllowed(source.dashboard?.hidden, DASHBOARD_WIDGETS);
  const profileId = source.display?.profile;
  const theme = source.display?.theme;
  const density = source.display?.density;
  const accent = source.display?.accent;
  const startView = source.queue?.startView;
  const severity = source.queue?.findingSeverity;
  const sort = source.queue?.sort;
  const alertMode = source.alerts?.mode;
  const quickActions = uniqueAllowed(source.quickActions, QUICK_ACTIONS, 4);
  const alerts = Object.fromEntries(ALERT_RULES.map((id) => [id, typeof source.alerts?.enabled?.[id] === 'boolean' ? source.alerts.enabled[id] : BASE_SNAPSHOT.alerts.enabled[id]])) as Record<AlertRuleId, boolean>;
  return {
    mobileTabs: mobileTabs.length > 0 ? mobileTabs : [...BASE_SNAPSHOT.mobileTabs],
    dashboard: {
      order: normalizedOrder,
      hidden,
    },
    display: {
      profile: profileId && profileId in DISPLAY_PROFILES ? profileId : BASE_SNAPSHOT.display.profile,
      theme: theme === 'light' || theme === 'dark' || theme === 'system' ? theme : BASE_SNAPSHOT.display.theme,
      density: density === 'compact' || density === 'comfortable' ? density : BASE_SNAPSHOT.display.density,
      textScale: typeof source.display?.textScale === 'number' && Number.isFinite(source.display.textScale)
        ? Math.min(1.24, Math.max(0.9, source.display.textScale))
        : BASE_SNAPSHOT.display.textScale,
      accent: accent && accent in ACCENT_COLORS ? accent : BASE_SNAPSHOT.display.accent,
      reducedMotion: Boolean(source.display?.reducedMotion),
      highContrast: Boolean(source.display?.highContrast),
      leftHanded: Boolean(source.display?.leftHanded),
    },
    queue: {
      startView: isViewId(startView) ? startView : BASE_SNAPSHOT.queue.startView,
      findingSeverity: severity === 'Error' || severity === 'Warning' || severity === 'Info' || severity === 'All' ? severity : BASE_SNAPSHOT.queue.findingSeverity,
      sort: sort === 'newest' || sort === 'severity' ? sort : BASE_SNAPSHOT.queue.sort,
      hideAccepted: Boolean(source.queue?.hideAccepted),
    },
    alerts: {
      mode: alertMode === 'digest' || alertMode === 'muted' || alertMode === 'immediate' ? alertMode : BASE_SNAPSHOT.alerts.mode,
      quietHours: typeof source.alerts?.quietHours === 'boolean' ? source.alerts.quietHours : BASE_SNAPSHOT.alerts.quietHours,
      enabled: alerts,
    },
    quickActions: quickActions.length > 0 ? quickActions : [...BASE_SNAPSHOT.quickActions],
  };
}

function normalizeProfile(value: Partial<WorkspaceProfile>, fallbackId: string): WorkspaceProfile {
  const timestamp = new Date().toISOString();
  return {
    id: typeof value.id === 'string' && value.id.trim() ? value.id : fallbackId,
    name: typeof value.name === 'string' && value.name.trim() ? value.name.trim().slice(0, 60) : 'Imported profile',
    description: typeof value.description === 'string' ? value.description.trim().slice(0, 180) : '',
    builtIn: Boolean(value.builtIn),
    createdAt: typeof value.createdAt === 'string' ? value.createdAt : timestamp,
    updatedAt: typeof value.updatedAt === 'string' ? value.updatedAt : timestamp,
    snapshot: normalizeSnapshot(value.snapshot),
  };
}

function mergeProfiles(values: unknown): WorkspaceProfile[] {
  const incoming = Array.isArray(values)
    ? values.map((item, index) => normalizeProfile(item as Partial<WorkspaceProfile>, `profile-imported-${index + 1}`))
    : [];
  const merged = new Map<string, WorkspaceProfile>(BUILT_IN_PROFILES.map((item) => [item.id, clone(item)]));
  incoming.forEach((item) => {
    if (!item.builtIn || !merged.has(item.id)) merged.set(item.id, item);
  });
  return [...merged.values()];
}

function normalizePreferences(value: Partial<UserPreferences> | undefined): UserPreferences {
  const source = value ?? {};
  const snapshot = normalizeSnapshot(source);
  const profiles = mergeProfiles(source.profiles);
  const activeProfileId = typeof source.activeProfileId === 'string' && profiles.some((item) => item.id === source.activeProfileId)
    ? source.activeProfileId
    : null;
  const role = source.onboarding?.role;
  return {
    version: 2,
    ...snapshot,
    activeProfileId,
    profiles,
    onboarding: {
      complete: Boolean(source.onboarding?.complete),
      role: role && role in ROLE_PROFILE_MAP ? role : null,
      completedAt: typeof source.onboarding?.completedAt === 'string' ? source.onboarding.completedAt : null,
    },
    portability: {
      lastExportAt: typeof source.portability?.lastExportAt === 'string' ? source.portability.lastExportAt : null,
      lastImportAt: typeof source.portability?.lastImportAt === 'string' ? source.portability.lastImportAt : null,
      lastImportSource: typeof source.portability?.lastImportSource === 'string' ? source.portability.lastImportSource : null,
    },
  };
}

function readMeta(): PreferenceMeta {
  try {
    const parsed = JSON.parse(localStorage.getItem(META_KEY) ?? '{}') as Partial<PreferenceMeta>;
    return {
      lastSavedAt: typeof parsed.lastSavedAt === 'string' ? parsed.lastSavedAt : null,
      backupSavedAt: typeof parsed.backupSavedAt === 'string' ? parsed.backupSavedAt : null,
      lastError: typeof parsed.lastError === 'string' ? parsed.lastError : null,
    };
  } catch {
    return { lastSavedAt: null, backupSavedAt: null, lastError: 'Preference metadata could not be parsed.' };
  }
}

function writeMeta(meta: PreferenceMeta): void {
  localStorage.setItem(META_KEY, JSON.stringify(meta));
}

function migrateLegacyPreferences(raw: string): UserPreferences {
  const legacy = JSON.parse(raw) as Partial<PreferenceSnapshot>;
  return normalizePreferences({
    ...legacy,
    version: 2,
    profiles: BUILT_IN_PROFILES,
    activeProfileId: null,
    onboarding: { complete: false, role: null, completedAt: null },
    portability: { lastExportAt: null, lastImportAt: null, lastImportSource: 'v8.2 migration' },
  });
}

export function loadUserPreferences(): UserPreferences {
  const meta = readMeta();
  const raw = localStorage.getItem(STORAGE_KEY);
  if (raw) {
    try {
      return normalizePreferences(JSON.parse(raw) as Partial<UserPreferences>);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Stored preferences could not be parsed.';
      writeMeta({ ...meta, lastError: message });
      const backup = localStorage.getItem(BACKUP_KEY);
      if (backup) {
        try {
          return normalizePreferences(JSON.parse(backup) as Partial<UserPreferences>);
        } catch {
          // Continue to legacy/default recovery.
        }
      }
    }
  }
  const legacy = localStorage.getItem(LEGACY_STORAGE_KEY);
  if (legacy) {
    try {
      const migrated = migrateLegacyPreferences(legacy);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(migrated));
      return migrated;
    } catch {
      writeMeta({ ...meta, lastError: 'Legacy preferences could not be migrated.' });
    }
  }
  return clone(DEFAULT_USER_PREFERENCES);
}

export function saveUserPreferences(preferences: UserPreferences): void {
  const normalized = normalizePreferences(preferences);
  const serialized = JSON.stringify(normalized);
  const current = localStorage.getItem(STORAGE_KEY);
  const timestamp = new Date().toISOString();
  const meta = readMeta();
  try {
    if (current && current !== serialized) {
      localStorage.setItem(BACKUP_KEY, current);
      meta.backupSavedAt = timestamp;
    }
    localStorage.setItem(STORAGE_KEY, serialized);
    writeMeta({ ...meta, lastSavedAt: timestamp, lastError: null });
  } catch (error) {
    writeMeta({ ...meta, lastError: error instanceof Error ? error.message : 'Preferences could not be saved.' });
  }
}

export function resetUserPreferences(): UserPreferences {
  const current = localStorage.getItem(STORAGE_KEY);
  const timestamp = new Date().toISOString();
  if (current) localStorage.setItem(BACKUP_KEY, current);
  localStorage.removeItem(STORAGE_KEY);
  writeMeta({ lastSavedAt: null, backupSavedAt: current ? timestamp : readMeta().backupSavedAt, lastError: null });
  return clone(DEFAULT_USER_PREFERENCES);
}

export function restorePreferenceBackup(): UserPreferences | null {
  const raw = localStorage.getItem(BACKUP_KEY);
  if (!raw) return null;
  try {
    const restored = normalizePreferences(JSON.parse(raw) as Partial<UserPreferences>);
    saveUserPreferences(restored);
    return restored;
  } catch (error) {
    const meta = readMeta();
    writeMeta({ ...meta, lastError: error instanceof Error ? error.message : 'Preference backup could not be restored.' });
    return null;
  }
}

export function applyDisplayProfile(preferences: UserPreferences, displayProfile: DisplayProfileId): UserPreferences {
  return {
    ...preferences,
    activeProfileId: null,
    display: {
      ...preferences.display,
      ...DISPLAY_PROFILES[displayProfile],
      profile: displayProfile,
    },
  };
}

export function snapshotPreferences(preferences: UserPreferences): PreferenceSnapshot {
  return normalizeSnapshot(preferences);
}

export function applyPreferenceSnapshot(preferences: UserPreferences, snapshot: PreferenceSnapshot, activeProfileId: string | null = null): UserPreferences {
  return {
    ...preferences,
    ...normalizeSnapshot(snapshot),
    activeProfileId,
  };
}

export function applyWorkspaceProfile(preferences: UserPreferences, profileId: string): UserPreferences {
  const selected = preferences.profiles.find((item) => item.id === profileId);
  return selected ? applyPreferenceSnapshot(preferences, selected.snapshot, selected.id) : preferences;
}

export function createWorkspaceProfile(preferences: UserPreferences, name: string, description = ''): UserPreferences {
  const timestamp = new Date().toISOString();
  const id = `profile-${timestamp.replace(/[^0-9]/g, '')}-${Math.random().toString(36).slice(2, 7)}`;
  const created: WorkspaceProfile = {
    id,
    name: name.trim().slice(0, 60) || 'My workspace',
    description: description.trim().slice(0, 180),
    builtIn: false,
    createdAt: timestamp,
    updatedAt: timestamp,
    snapshot: snapshotPreferences(preferences),
  };
  return { ...preferences, profiles: [...preferences.profiles, created], activeProfileId: created.id };
}

export function updateWorkspaceProfile(preferences: UserPreferences, profileId: string): UserPreferences {
  return {
    ...preferences,
    profiles: preferences.profiles.map((item) => item.id === profileId && !item.builtIn
      ? { ...item, snapshot: snapshotPreferences(preferences), updatedAt: new Date().toISOString() }
      : item),
  };
}

export function deleteWorkspaceProfile(preferences: UserPreferences, profileId: string): UserPreferences {
  const selected = preferences.profiles.find((item) => item.id === profileId);
  if (!selected || selected.builtIn) return preferences;
  return {
    ...preferences,
    profiles: preferences.profiles.filter((item) => item.id !== profileId),
    activeProfileId: preferences.activeProfileId === profileId ? null : preferences.activeProfileId,
  };
}

export function applyRolePreset(preferences: UserPreferences, role: WorkspaceRoleId, complete = true): UserPreferences {
  const profileId = ROLE_PROFILE_MAP[role];
  const applied = applyWorkspaceProfile(preferences, profileId);
  return {
    ...applied,
    onboarding: {
      complete,
      role,
      completedAt: complete ? new Date().toISOString() : null,
    },
  };
}

export function restartOnboarding(preferences: UserPreferences): UserPreferences {
  return { ...preferences, onboarding: { ...preferences.onboarding, complete: false, completedAt: null } };
}

function checksumFor(value: unknown): string {
  const text = JSON.stringify(value);
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return (hash >>> 0).toString(16).padStart(8, '0');
}

function packagePayload(pkg: Omit<PreferencesPackage, 'checksum'>): Omit<PreferencesPackage, 'checksum'> {
  return pkg;
}

export function createPreferencesPackage(preferences: UserPreferences, categories: PreferenceCategory[] = PREFERENCE_CATEGORIES, deviceLabel = 'Workbench Studio device'): PreferencesPackage {
  const selected = uniqueAllowed(categories, PREFERENCE_CATEGORIES);
  const snapshot = snapshotPreferences(preferences);
  const portable: Partial<PreferenceSnapshot> = {};
  if (selected.includes('navigation')) portable.mobileTabs = snapshot.mobileTabs;
  if (selected.includes('dashboard')) portable.dashboard = snapshot.dashboard;
  if (selected.includes('display')) portable.display = snapshot.display;
  if (selected.includes('queue')) portable.queue = snapshot.queue;
  if (selected.includes('alerts')) portable.alerts = snapshot.alerts;
  if (selected.includes('quickActions')) portable.quickActions = snapshot.quickActions;
  const withoutChecksum: Omit<PreferencesPackage, 'checksum'> = {
    format: 'workbench-studio-preferences',
    packageVersion: 1,
    schemaVersion: 2,
    appVersion: '0.8.4',
    exportedAt: new Date().toISOString(),
    deviceLabel: deviceLabel.trim().slice(0, 80) || 'Workbench Studio device',
    categories: selected,
    preferences: portable,
    profiles: selected.includes('profiles') ? preferences.profiles.filter((item) => !item.builtIn).map(clone) : [],
  };
  return { ...withoutChecksum, checksum: checksumFor(packagePayload(withoutChecksum)) };
}

export function serializePreferencesPackage(pkg: PreferencesPackage): string {
  return `${JSON.stringify(pkg, null, 2)}\n`;
}

export function parsePreferencesPackage(raw: string): PreferencesPackage {
  const parsed = JSON.parse(raw) as Partial<PreferencesPackage>;
  if (parsed.format !== 'workbench-studio-preferences' || parsed.packageVersion !== 1 || parsed.schemaVersion !== 2) {
    throw new Error('This file is not a supported Workbench Studio portable preference package.');
  }
  const categories = uniqueAllowed(parsed.categories, PREFERENCE_CATEGORIES);
  if (categories.length === 0) throw new Error('The preference package does not contain importable categories.');
  const withoutChecksum: Omit<PreferencesPackage, 'checksum'> = {
    format: 'workbench-studio-preferences',
    packageVersion: 1,
    schemaVersion: 2,
    appVersion: typeof parsed.appVersion === 'string' ? parsed.appVersion : '0.8.3',
    exportedAt: typeof parsed.exportedAt === 'string' ? parsed.exportedAt : new Date().toISOString(),
    deviceLabel: typeof parsed.deviceLabel === 'string' ? parsed.deviceLabel : 'Imported device',
    categories,
    preferences: parsed.preferences ?? {},
    profiles: Array.isArray(parsed.profiles) ? parsed.profiles.map((item, index) => normalizeProfile(item, `profile-imported-${index + 1}`)) : [],
  };
  const expected = checksumFor(packagePayload(withoutChecksum));
  if (parsed.checksum !== expected) throw new Error('Preference package integrity validation failed.');
  return { ...withoutChecksum, checksum: expected };
}

export function applyPreferencesPackage(current: UserPreferences, pkg: PreferencesPackage, categories: PreferenceCategory[] = pkg.categories): UserPreferences {
  const selected = uniqueAllowed(categories, pkg.categories);
  const snapshot = snapshotPreferences(current);
  const incoming = pkg.preferences;
  const nextSnapshot: PreferenceSnapshot = {
    mobileTabs: selected.includes('navigation') && incoming.mobileTabs ? incoming.mobileTabs : snapshot.mobileTabs,
    dashboard: selected.includes('dashboard') && incoming.dashboard ? incoming.dashboard : snapshot.dashboard,
    display: selected.includes('display') && incoming.display ? incoming.display : snapshot.display,
    queue: selected.includes('queue') && incoming.queue ? incoming.queue : snapshot.queue,
    alerts: selected.includes('alerts') && incoming.alerts ? incoming.alerts : snapshot.alerts,
    quickActions: selected.includes('quickActions') && incoming.quickActions ? incoming.quickActions : snapshot.quickActions,
  };
  const profiles = selected.includes('profiles') ? mergeProfiles([...current.profiles, ...pkg.profiles]) : current.profiles;
  return {
    ...current,
    ...normalizeSnapshot(nextSnapshot),
    profiles,
    activeProfileId: null,
    portability: {
      ...current.portability,
      lastImportAt: new Date().toISOString(),
      lastImportSource: pkg.deviceLabel,
    },
  };
}

export function markPreferencesExported(preferences: UserPreferences): UserPreferences {
  return {
    ...preferences,
    portability: { ...preferences.portability, lastExportAt: new Date().toISOString() },
  };
}

export function resetPreferenceCategory(preferences: UserPreferences, category: PreferenceCategory): UserPreferences {
  const defaults = snapshotPreferences(DEFAULT_USER_PREFERENCES);
  switch (category) {
    case 'navigation': return { ...preferences, mobileTabs: defaults.mobileTabs, activeProfileId: null };
    case 'dashboard': return { ...preferences, dashboard: defaults.dashboard, activeProfileId: null };
    case 'display': return { ...preferences, display: defaults.display, activeProfileId: null };
    case 'queue': return { ...preferences, queue: defaults.queue, activeProfileId: null };
    case 'alerts': return { ...preferences, alerts: defaults.alerts, activeProfileId: null };
    case 'quickActions': return { ...preferences, quickActions: defaults.quickActions, activeProfileId: null };
    case 'profiles': return { ...preferences, profiles: clone(BUILT_IN_PROFILES), activeProfileId: null };
    default: return preferences;
  }
}

export function getPreferenceDiagnostics(preferences: UserPreferences): PreferenceDiagnostics {
  const raw = localStorage.getItem(STORAGE_KEY) ?? '';
  const meta = readMeta();
  return {
    schemaVersion: 2,
    storageKey: STORAGE_KEY,
    storageBytes: new Blob([raw]).size,
    lastSavedAt: meta.lastSavedAt,
    backupAvailable: Boolean(localStorage.getItem(BACKUP_KEY)),
    backupSavedAt: meta.backupSavedAt,
    profileCount: preferences.profiles.length,
    onboardingComplete: preferences.onboarding.complete,
    activeProfileId: preferences.activeProfileId,
    lastExportAt: preferences.portability.lastExportAt,
    lastImportAt: preferences.portability.lastImportAt,
    lastImportSource: preferences.portability.lastImportSource,
    lastError: meta.lastError,
  };
}

export function serializePreferenceDiagnostics(preferences: UserPreferences): string {
  return `${JSON.stringify({
    product: 'Workbench Studio',
    version: '0.8.4',
    generatedAt: new Date().toISOString(),
    diagnostics: getPreferenceDiagnostics(preferences),
    categories: PREFERENCE_CATEGORIES,
    continuousCloudSync: 'disabled-until-authenticated-provider-is-configured',
    evidenceIncluded: false,
  }, null, 2)}\n`;
}
