# Changelog

## 0.6.0 — 2026-08-01

### Continuous evidence operations

- Added persisted watch-folder definitions with manual, hourly, and daily scan modes.
- Added metadata fingerprinting and automatic immutable snapshot creation.
- Added a background watch worker with an hourly minimum automatic cadence.
- Added ignore patterns, approval flags, pause/resume, scan-now, and last-import state.

### Data intelligence

- Added persisted data profiles for CSV, JSON, XML, XLSX, logs, and text evidence.
- Added blanks, duplicates, row-width drift, structure depth, formula-summary, and parser-failure signals.
- Added persisted lineage edges for containment, duplicate content, cross-file references, and finding evidence.

### Investigation workflows

- Added persisted investigation playbooks with ordered steps, progress, status, and run summaries.
- Added Agent Setup, Watch Folders, Profiler, Impact Studio, Playbooks, and Privacy Center to the hosted experience.
- Added a React Operations Center connected to the local-agent APIs.

### Privacy

- Added local sensitive-value candidate detection.
- Added masked evidence previews and persisted review status.
- Added redacted derivative ZIP exports for supported text artifacts.

### Compatibility

- Retained the Safari-compatible direct-source Vercel loader; no browser gzip or base64 decoding is required.
