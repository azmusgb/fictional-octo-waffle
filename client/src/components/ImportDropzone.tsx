import { useRef, useState, type ChangeEvent, type DragEvent } from 'react';
import { formatBytes } from '../lib/format';

interface ImportDropzoneProps {
  disabled?: boolean;
  busy?: boolean;
  onImport: (files: File[], displayName: string) => Promise<void>;
}

const acceptedExtensions = '.zip,.json,.csv,.xml,.log,.txt,.out,.trace,.xlsx,.xls,.pdf';

export function ImportDropzone({ disabled = false, busy = false, onImport }: ImportDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [displayName, setDisplayName] = useState('');
  const [dragActive, setDragActive] = useState(false);

  function mergeFiles(nextFiles: File[]) {
    const keyed = new Map<string, File>();
    for (const file of [...files, ...nextFiles]) {
      keyed.set(`${file.name}:${file.size}:${file.lastModified}`, file);
    }
    setFiles([...keyed.values()]);
  }

  function handleInput(event: ChangeEvent<HTMLInputElement>) {
    mergeFiles(Array.from(event.target.files ?? []));
    event.target.value = '';
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setDragActive(false);
    if (disabled || busy) return;
    mergeFiles(Array.from(event.dataTransfer.files));
  }

  async function submit() {
    if (files.length === 0 || disabled || busy) return;
    await onImport(files, displayName);
    setFiles([]);
    setDisplayName('');
  }

  return (
    <section className="import-card" aria-labelledby="import-heading">
      <div className="section-heading compact-heading">
        <div>
          <span className="eyebrow">New snapshot</span>
          <h2 id="import-heading">Import project artifacts</h2>
        </div>
        <span className="quiet-label">Local processing</span>
      </div>

      <div
        className={`dropzone${dragActive ? ' dropzone-active' : ''}${disabled ? ' is-disabled' : ''}`}
        onDragEnter={(event: DragEvent<HTMLDivElement>) => {
          event.preventDefault();
          if (!disabled && !busy) setDragActive(true);
        }}
        onDragOver={(event: DragEvent<HTMLDivElement>) => event.preventDefault()}
        onDragLeave={(event: DragEvent<HTMLDivElement>) => {
          if (event.currentTarget === event.target) setDragActive(false);
        }}
        onDrop={handleDrop}
      >
        <div className="dropzone-icon" aria-hidden="true">⇩</div>
        <strong>Drop ZIP, JSON, CSV, XML, Excel, logs, or document exports</strong>
        <span>JSON, CSV, XML, XLSX, and text/log files are analyzed locally. Imported snapshots remain immutable.</span>
        <button
          type="button"
          className="secondary-button"
          disabled={disabled || busy}
          onClick={() => inputRef.current?.click()}
        >
          Select files
        </button>
        <input
          ref={inputRef}
          type="file"
          multiple
          accept={acceptedExtensions}
          onChange={handleInput}
          hidden
        />
      </div>

      {files.length > 0 ? (
        <div className="selected-files" aria-live="polite">
          <div className="selected-files-header">
            <strong>{files.length} selected file{files.length === 1 ? '' : 's'}</strong>
            <button type="button" className="text-button" onClick={() => setFiles([])} disabled={busy}>
              Clear
            </button>
          </div>
          <ul>
            {files.slice(0, 5).map((file) => (
              <li key={`${file.name}:${file.size}:${file.lastModified}`}>
                <span>{file.name}</span>
                <span>{formatBytes(file.size)}</span>
              </li>
            ))}
          </ul>
          {files.length > 5 ? <p className="muted-text">and {files.length - 5} more</p> : null}
          <div className="import-actions">
            <label className="field-label">
              Snapshot name <span className="optional-label">optional</span>
              <input
                value={displayName}
                onChange={(event: ChangeEvent<HTMLInputElement>) => setDisplayName(event.target.value)}
                maxLength={240}
                placeholder="Example: Production export — August 1"
                disabled={busy}
              />
            </label>
            <button type="button" className="primary-button" onClick={() => void submit()} disabled={busy}>
              {busy ? 'Uploading…' : 'Create snapshot'}
            </button>
          </div>
        </div>
      ) : null}
    </section>
  );
}
