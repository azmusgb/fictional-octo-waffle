# Workbench Studio v6

Workbench Studio is a local-first evidence operations and investigation application. It combines a React/TypeScript shell, an ASP.NET Core processing agent, SQLite metadata, disk-backed immutable artifacts, safe format parsers, human review, structured comparison, continuous intake, quality profiling, impact analysis, repeatable playbooks, and privacy-safe exports.

## v6 release objective

Version 6 turns Workbench Studio from a manually operated investigation tool into a continuously useful evidence system:

1. Connect and validate the local processing agent.
2. Watch approved local folders for meaningful changes.
3. Create immutable snapshots without repeated manual uploads.
4. Profile data quality before opening individual records.
5. Trace changes through findings, cases, and derived outputs.
6. Run repeatable investigation playbooks.
7. Detect sensitive-value candidates and generate redacted derivatives.

## Signature v6 functionality

### Agent setup and recovery

- Localhost discovery workflow
- Shell/agent major-version compatibility check
- Workspace selection and validation
- SQLite, original-file, and available-capacity checks
- Last-project restoration and reconnect states

### Watch folders and automatic snapshots

- Persisted local watch-folder definitions
- Manual, hourly, and daily modes
- Ignore patterns and approval gates
- Metadata fingerprints for change detection
- Safe ZIP staging of folder contents
- Immutable import creation through the existing import queue
- Background scan worker with a one-minute scheduler and an hourly minimum automatic cadence

### Data Quality Profiler

- Persisted per-artifact profiles
- CSV row, column, blank-cell, duplicate-row, and row-width metrics
- JSON node, depth, object, and array metrics
- XML element, attribute, and unique-name metrics
- Log line, warning, and error counts
- XLSX parser-summary reuse
- Duplicate-content and parser-failure issues

### Lineage and Impact Studio

- Archive containment edges
- SHA-256 duplicate-content edges
- Cross-file filename-reference edges
- Finding-to-source evidence edges
- Rebuildable persisted lineage graph
- Impact summaries for changed sources and downstream outputs

### Investigation Playbooks

- Persisted playbook definitions and ordered steps
- Profile, privacy, lineage/impact, and review step types
- Run status, progress, timestamp, and summary
- Starter workflows for evidence readiness and regression review

### Privacy and Redaction Center

- Local pattern detection for SSNs, email addresses, phone numbers, payment-card candidates, and API-key candidates
- Masked previews and source locations
- Persisted detection status
- Redacted derivative ZIP export for supported text artifacts
- Immutable originals remain untouched

## Existing investigation functionality retained

- Evidence Workstation
- Import Control Room
- Caseboard
- Query Lab
- Validation Rule Studio
- Operational Review Queue
- Snapshot Diff Studio
- Relationship Map
- Activity Timeline
- Saved views
- Reports and project manifest
- Theme, density, command palette, focus mode, tour, and undo

## Local architecture

- UI: React 19 + TypeScript + Vite
- Local processing agent: ASP.NET Core targeting .NET 10
- Metadata: SQLite
- Artifact storage: disk-backed originals, extraction cache, and exports
- Parsers: JSON, CSV, XML, text/log, and XLSX
- Tests: xUnit and Vitest

## Run locally

```powershell
.\scripts\bootstrap.ps1
.\scripts\run-dev.ps1
```

Open `http://localhost:5173`.

## Build and test

```powershell
.\scripts\build.ps1
```

The build script restores dependencies, runs backend and frontend tests, builds the Vite client, copies it into the ASP.NET `wwwroot`, and publishes a combined local release.

## Safety boundary

- Imported content is never executed.
- ZIP extraction is path-bounded and resource-limited.
- XML DTD processing and external resolution are prohibited.
- Watch folders stage a ZIP copy; source folders are never modified.
- Privacy exports create redacted derivatives and retain original evidence.
- Hosted demo data stays in browser storage unless the user connects the local agent.
