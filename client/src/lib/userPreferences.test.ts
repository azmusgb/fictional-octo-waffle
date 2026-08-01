import { describe, expect, it } from 'vitest';
import { applyDisplayProfile, DEFAULT_USER_PREFERENCES } from './userPreferences';

describe('user preferences', () => {
  it('ships with four recommended mobile destinations', () => {
    expect(DEFAULT_USER_PREFERENCES.mobileTabs).toEqual(['overview', 'inventory', 'review', 'command']);
  });

  it('applies accessibility profiles without changing navigation or queue policy', () => {
    const next = applyDisplayProfile(DEFAULT_USER_PREFERENCES, 'high-contrast');
    expect(next.display.profile).toBe('high-contrast');
    expect(next.display.highContrast).toBe(true);
    expect(next.display.reducedMotion).toBe(true);
    expect(next.mobileTabs).toEqual(DEFAULT_USER_PREFERENCES.mobileTabs);
    expect(next.queue).toEqual(DEFAULT_USER_PREFERENCES.queue);
  });
});
