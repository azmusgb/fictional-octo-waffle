import { describe, expect, it } from 'vitest';
import { formatBytes, isActiveImport, progressPercent } from './format';

describe('format helpers', () => {
  it('formats byte sizes', () => {
    expect(formatBytes(0)).toBe('0 B');
    expect(formatBytes(1024)).toBe('1.00 KB');
    expect(formatBytes(10 * 1024)).toBe('10.0 KB');
  });

  it('clamps progress', () => {
    expect(progressPercent(5, 10)).toBe(50);
    expect(progressPercent(20, 10)).toBe(100);
    expect(progressPercent(1, 0)).toBe(0);
  });

  it('identifies active import states', () => {
    expect(isActiveImport('Parsing')).toBe(true);
    expect(isActiveImport('Completed')).toBe(false);
    expect(isActiveImport('Failed')).toBe(false);
  });
});
