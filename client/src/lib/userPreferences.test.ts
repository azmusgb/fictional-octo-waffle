import { describe, expect, it } from 'vitest';
import {
  applyDisplayProfile,
  applyPreferencesPackage,
  applyRolePreset,
  applyWorkspaceProfile,
  createPreferencesPackage,
  createWorkspaceProfile,
  DEFAULT_USER_PREFERENCES,
  parsePreferencesPackage,
  serializePreferencesPackage,
} from './userPreferences';

describe('user preferences', () => {
  it('ships with four recommended mobile destinations and built-in profiles', () => {
    expect(DEFAULT_USER_PREFERENCES.mobileTabs).toEqual(['overview', 'inventory', 'review', 'command']);
    expect(DEFAULT_USER_PREFERENCES.profiles.length).toBeGreaterThanOrEqual(5);
  });

  it('applies accessibility profiles without changing navigation or queue policy', () => {
    const next = applyDisplayProfile(DEFAULT_USER_PREFERENCES, 'high-contrast');
    expect(next.display.profile).toBe('high-contrast');
    expect(next.display.highContrast).toBe(true);
    expect(next.display.reducedMotion).toBe(true);
    expect(next.mobileTabs).toEqual(DEFAULT_USER_PREFERENCES.mobileTabs);
    expect(next.queue).toEqual(DEFAULT_USER_PREFERENCES.queue);
  });

  it('applies a role preset and completes guided setup', () => {
    const next = applyRolePreset(DEFAULT_USER_PREFERENCES, 'approver');
    expect(next.onboarding.complete).toBe(true);
    expect(next.onboarding.role).toBe('approver');
    expect(next.queue.startView).toBe('decisions');
  });

  it('creates and reapplies a named custom profile', () => {
    const created = createWorkspaceProfile(DEFAULT_USER_PREFERENCES, 'My review mode');
    const custom = created.profiles.find((item) => item.name === 'My review mode');
    expect(custom?.builtIn).toBe(false);
    expect(custom ? applyWorkspaceProfile({ ...created, mobileTabs: ['system'] }, custom.id).mobileTabs : []).toEqual(DEFAULT_USER_PREFERENCES.mobileTabs);
  });

  it('round-trips a checksum-protected portable package', () => {
    const pkg = createPreferencesPackage(DEFAULT_USER_PREFERENCES, ['navigation', 'display', 'profiles'], 'Test device');
    const parsed = parsePreferencesPackage(serializePreferencesPackage(pkg));
    const applied = applyPreferencesPackage({ ...DEFAULT_USER_PREFERENCES, mobileTabs: ['system'] }, parsed, ['navigation']);
    expect(parsed.deviceLabel).toBe('Test device');
    expect(applied.mobileTabs).toEqual(DEFAULT_USER_PREFERENCES.mobileTabs);
  });
});
