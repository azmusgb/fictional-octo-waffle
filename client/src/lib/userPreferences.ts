import type { ViewId } from './types';

export type AccentId = 'blue' | 'purple' | 'teal' | 'gold' | 'rose';
export type DisplayProfileId = 'standard' | 'large-touch' | 'high-contrast' | 'dense-review' | 'evidence-reading';
export type DashboardWidgetId = 'metrics' | 'processing' | 'intake' | 'insights';
export type QuickActionId = 'review' | 'findings' | 'compare' | 'command' | 'operations' | 'exports' | 'customize';
export type AlertRuleId = 'criticalFinding' | 'approvalAssigned' | 'privacyDetected' | 'baselineRegression' | 'automationFailed' | 'agentDisconnected';

export interface UserPreferences {
  version: 1;
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

export const ACCENT_COLORS: Record<AccentId, { base: string; strong: string; soft: string }> = {
  blue: { base: '#5B8CFF', strong: '#3566D7', soft: '#EAF0FF' },
  purple: { base: '#7C5CFC', strong: '#5A3ED8', soft: '#F0ECFF' },
  teal: { base: '#21B89A', strong: '#087E6B', soft: '#E5F8F4' },
  gold: { base: '#F2A93B', strong: '#A96500', soft: '#FFF4DF' },
  rose: { base: '#E45B73', strong: '#B62D4A', soft: '#FDEBF0' },
};

export const DISPLAY_PROFILES: Record<DisplayProfileId, Pick<UserPreferences['display'], 'density' | 'textScale' | 'reducedMotion' | 'highContrast'>> = {
  standard: { density: 'comfortable', textScale: 1, reducedMotion: false, highContrast: false },
  'large-touch': { density: 'comfortable', textScale: 1.08, reducedMotion: false, highContrast: false },
  'high-contrast': { density: 'comfortable', textScale: 1.04, reducedMotion: true, highContrast: true },
  'dense-review': { density: 'compact', textScale: 0.96, reducedMotion: false, highContrast: false },
  'evidence-reading': { density: 'comfortable', textScale: 1.12, reducedMotion: true, highContrast: false },
};

export const DEFAULT_USER_PREFERENCES: UserPreferences = {
  version: 1,
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

const STORAGE_KEY = 'ws.userPreferences.v1';

export function loadUserPreferences(): UserPreferences {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return structuredClone(DEFAULT_USER_PREFERENCES);
    const parsed = JSON.parse(raw) as Partial<UserPreferences>;
    const mobileTabs = Array.isArray(parsed.mobileTabs)
      ? parsed.mobileTabs.filter((item): item is ViewId => typeof item === 'string').slice(0, 4)
      : [];
    const dashboardOrder = Array.isArray(parsed.dashboard?.order)
      ? parsed.dashboard.order.filter((item): item is DashboardWidgetId => ['metrics', 'processing', 'intake', 'insights'].includes(item))
      : [];
    return {
      ...structuredClone(DEFAULT_USER_PREFERENCES),
      ...parsed,
      mobileTabs: mobileTabs.length > 0 && mobileTabs.length <= 4 ? mobileTabs : [...DEFAULT_USER_PREFERENCES.mobileTabs],
      dashboard: {
        ...DEFAULT_USER_PREFERENCES.dashboard,
        ...parsed.dashboard,
        order: dashboardOrder.length === 4 ? dashboardOrder : [...DEFAULT_USER_PREFERENCES.dashboard.order],
        hidden: Array.isArray(parsed.dashboard?.hidden) ? parsed.dashboard.hidden : [],
      },
      display: { ...DEFAULT_USER_PREFERENCES.display, ...parsed.display },
      queue: { ...DEFAULT_USER_PREFERENCES.queue, ...parsed.queue },
      alerts: {
        ...DEFAULT_USER_PREFERENCES.alerts,
        ...parsed.alerts,
        enabled: { ...DEFAULT_USER_PREFERENCES.alerts.enabled, ...parsed.alerts?.enabled },
      },
      quickActions: Array.isArray(parsed.quickActions) && parsed.quickActions.length > 0
        ? parsed.quickActions.slice(0, 4)
        : [...DEFAULT_USER_PREFERENCES.quickActions],
      version: 1,
    };
  } catch {
    return structuredClone(DEFAULT_USER_PREFERENCES);
  }
}

export function saveUserPreferences(preferences: UserPreferences): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences));
}

export function resetUserPreferences(): UserPreferences {
  localStorage.removeItem(STORAGE_KEY);
  return structuredClone(DEFAULT_USER_PREFERENCES);
}

export function applyDisplayProfile(preferences: UserPreferences, profile: DisplayProfileId): UserPreferences {
  return {
    ...preferences,
    display: {
      ...preferences.display,
      ...DISPLAY_PROFILES[profile],
      profile,
    },
  };
}
