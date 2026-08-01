interface StatusBadgeProps {
  value: string;
  compact?: boolean;
}

const toneMap: Record<string, string> = {
  Completed: 'success',
  Parsed: 'success',
  Unchanged: 'neutral',
  CompletedWithWarnings: 'warning',
  ParsedWithWarnings: 'warning',
  Warning: 'warning',
  Modified: 'warning',
  Failed: 'danger',
  Error: 'danger',
  Removed: 'danger',
  Cancelled: 'neutral',
  Unsupported: 'neutral',
  Info: 'info',
  Added: 'info',
  Queued: 'info',
  Preparing: 'info',
  Extracting: 'info',
  Inventorying: 'info',
  Parsing: 'info',
  Validating: 'info',
  Indexing: 'info',
  Healthy: 'success',
  Accepted: 'success',
  NeedsAttention: 'danger',
  InReview: 'info',
  Unreviewed: 'neutral',
  Disconnected: 'danger',
};

export function StatusBadge({ value, compact = false }: StatusBadgeProps) {
  const tone = toneMap[value] ?? 'neutral';
  const label = value.replace(/([a-z])([A-Z])/g, '$1 $2');
  return (
    <span className={`status-badge status-${tone}${compact ? ' status-compact' : ''}`}>
      <span className="status-dot" aria-hidden="true" />
      {label}
    </span>
  );
}
