import { useCallback, useEffect, useMemo, useState, type ChangeEvent } from 'react';
import { api } from '../lib/api';
import { formatBytes, formatDate } from '../lib/format';
import type { AgentStatus } from '../lib/types';
import { StatusBadge } from './StatusBadge';

interface SystemCenterProps {
  initialStatus?: AgentStatus | null;
}

export function SystemCenter({ initialStatus = null }: SystemCenterProps) {
  const [status, setStatus] = useState<AgentStatus | null>(initialStatus);
  const [error, setError] = useState<string | null>(null);
  const [baseUrl, setBaseUrl] = useState(api.baseUrl);
  const [loading, setLoading] = useState(!initialStatus);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setStatus(await api.getAgentStatus());
    } catch (reason) {
      setStatus(null);
      setError(reason instanceof Error ? reason.message : 'Local agent unavailable.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!initialStatus) void refresh();
  }, [initialStatus, refresh]);

  const connectionMode = useMemo(() => {
    if (!api.baseUrl) return 'Same-origin shell and agent';
    try {
      const target = new URL(api.baseUrl, window.location.href);
      return target.origin === window.location.origin ? 'Same-origin shell and agent' : 'Remote shell → local agent';
    } catch {
      return 'Custom agent connection';
    }
  }, []);

  function saveConnection() {
    const normalized = baseUrl.trim().replace(/\/$/, '');
    if (normalized) localStorage.setItem('ws.apiBaseUrl', normalized);
    else localStorage.removeItem('ws.apiBaseUrl');
    window.location.reload();
  }

  return (
    <div className="system-grid">
      <section className="surface-card system-card system-connection-card">
        <div className="section-heading">
          <div><span className="eyebrow">Connection</span><h2>Local processing agent</h2></div>
          <div className="system-heading-actions">
            {loading ? <span className="quiet-label">Checking…</span> : <StatusBadge value={status?.status ?? 'Disconnected'} compact />}
            <button type="button" className="secondary-button compact-button" onClick={() => void refresh()} disabled={loading}>Refresh</button>
          </div>
        </div>
        <p>The browser shell may run locally or on Vercel. Parsing, SQLite, originals, reviews, and evidence remain inside the ASP.NET Core agent.</p>
        <div className="connection-mode"><span>Mode</span><strong>{connectionMode}</strong></div>
        <label className="field-label">Agent base URL<input value={baseUrl} onChange={(event: ChangeEvent<HTMLInputElement>) => setBaseUrl(event.target.value)} placeholder="Blank for same-origin, or http://localhost:5087" autoCapitalize="none" autoCorrect="off" spellCheck={false} /></label>
        <div className="system-actions"><button type="button" className="primary-button" onClick={saveConnection}>Save and reconnect</button><button type="button" className="secondary-button" onClick={() => setBaseUrl('')}>Use same origin</button></div>
        {error ? <div className="inline-error" role="alert"><strong>Agent connection failed</strong><span>{error}</span><small>Start the local agent, confirm the URL, and add the exact browser-shell origin to Workspace:AllowedOrigins.</small></div> : null}
      </section>

      {status ? (
        <>
          <section className="surface-card system-card">
            <span className="eyebrow">Runtime</span><h2>{status.service}</h2>
            <dl className="property-list">
              <div><dt>Version</dt><dd>{status.version}</dd></div>
              <div><dt>Last checked</dt><dd>{formatDate(status.timestampUtc)}</dd></div>
              <div><dt>Uptime</dt><dd>{formatDuration(status.uptimeSeconds)}</dd></div>
              <div><dt>Database</dt><dd>{formatBytes(status.databaseSizeBytes)}</dd></div>
              <div><dt>Workspace free</dt><dd>{formatBytes(status.workspaceFreeBytes)}</dd></div>
              <div><dt>Workspace capacity</dt><dd>{formatBytes(status.workspaceTotalBytes)}</dd></div>
            </dl>
          </section>

          <section className="surface-card system-card">
            <span className="eyebrow">Evidence inventory</span><h2>Operational counters</h2>
            <div className="system-counter-grid">
              <div><strong>{status.projectCount.toLocaleString()}</strong><span>Projects</span></div>
              <div><strong>{status.importCount.toLocaleString()}</strong><span>Snapshots</span></div>
              <div><strong>{status.artifactCount.toLocaleString()}</strong><span>Artifacts</span></div>
              <div><strong>{status.findingCount.toLocaleString()}</strong><span>Findings</span></div>
            </div>
            <div className={status.queuedImportCount > 0 ? 'agent-queue-note is-active' : 'agent-queue-note'}><span aria-hidden="true" />{status.queuedImportCount.toLocaleString()} queued or active import{status.queuedImportCount === 1 ? '' : 's'}</div>
          </section>

          <section className="surface-card system-card">
            <span className="eyebrow">Parser registry</span><h2>{status.parsers.length} active parsers</h2>
            <div className="capability-list">{status.parsers.map((parser) => <code key={parser}>{parser}</code>)}</div>
          </section>

          <section className="surface-card system-card">
            <span className="eyebrow">Safety envelope</span><h2>Ingestion limits</h2>
            <dl className="property-list">
              <div><dt>Upload</dt><dd>{formatBytes(status.limits.maximumUploadBytes)}</dd></div>
              <div><dt>Single file</dt><dd>{formatBytes(status.limits.maximumSingleFileBytes)}</dd></div>
              <div><dt>Extracted data</dt><dd>{formatBytes(status.limits.maximumExtractedBytes)}</dd></div>
              <div><dt>Extracted files</dt><dd>{status.limits.maximumExtractedFiles.toLocaleString()}</dd></div>
              <div><dt>Compression ratio</dt><dd>{status.limits.maximumCompressionRatio.toLocaleString()}:1</dd></div>
            </dl>
          </section>

          <section className="surface-card system-card system-security-card">
            <span className="eyebrow">Boundary</span><h2>Hosted-shell safeguards</h2>
            <ul className="system-checklist">
              <li>Only explicitly configured origins may call the local agent.</li>
              <li>Private-network preflight is answered only after CORS origin validation.</li>
              <li>Originals and SQLite are never uploaded to Vercel by this application.</li>
              <li>The hosted shell contains presentation code only; processing remains local.</li>
            </ul>
          </section>
        </>
      ) : null}
    </div>
  );
}

function formatDuration(totalSeconds: number): string {
  const days = Math.floor(totalSeconds / 86_400);
  const hours = Math.floor((totalSeconds % 86_400) / 3_600);
  const minutes = Math.floor((totalSeconds % 3_600) / 60);
  if (days > 0) return `${days}d ${hours}h ${minutes}m`;
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}
