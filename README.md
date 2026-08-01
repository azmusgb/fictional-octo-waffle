# Workbench Studio v5

Workbench Studio is a local-first evidence and workflow investigation application. It combines a React/TypeScript shell, an ASP.NET Core processing agent, SQLite metadata, disk-backed original artifacts, safe parsers, validation, review workflows, comparison, reporting, and a dependency-free hosted experience.

## Live hosted experience

Production: https://workbench-studio-v5.vercel.app

The hosted adapter in `hosted/index.html` is fully interactive in demo mode and persists its working state in browser `localStorage`. It does not upload original artifacts to Vercel.

### v5 signature capabilities

- **Investigation Caseboard**
  - Pin artifacts and evidence into named cases
  - Maintain hypotheses, confidence, notes, and follow-up actions
  - Reopen or complete case tasks
  - Export a portable case brief
- **Cross-file Query Lab**
  - Run deterministic searches over paths, types, review state, tags, and findings
  - Use query recipes and formatted query output
  - Save query results as reusable views
  - Send query result sets directly into review
- **Validation Rule Studio**
  - Enable or disable validation rules
  - Run the active rule set against the demo workspace
  - Inspect rule outcomes and severity summaries
  - Create additional local rule definitions
- **Evidence Workstation**
  - Artifact tree and exact source navigation
  - JSON, CSV, XML, XLSX, log, and text representations
  - Structured, preview, raw, and relationship modes
  - Evidence inspector with provenance, hash, review state, notes, and tags
- **Import Control Room**
  - Drag-and-drop, preflight metrics, safety stages, progress, pause, resume, cancel, and completion routing
- **Review and comparison**
  - Operational review queues, bulk actions, saved review state, keyboard shortcuts, and undo
  - File, structured, and tabular snapshot comparison modes
- **Workbench productivity**
  - Command palette (`Ctrl+K` / `Cmd+K`)
  - Focus mode (`F`)
  - Guided first-run tour
  - Relationship map, activity timeline, saved views, exports, theme, and density controls

## Local production architecture

- UI: React 19 + TypeScript + Vite
- Processing agent: ASP.NET Core
- Storage: SQLite metadata plus disk-backed originals, extracted artifacts, caches, and exports
- Parsers: JSON, CSV, XML, text/log, and XLSX
- Tests: xUnit, Vitest, and source fixtures

The ASP.NET Core agent remains authoritative for real imports, parsing, immutable snapshots, evidence, findings, persisted review decisions, comparison, and report generation.

## Run on Windows

```powershell
.\scripts\bootstrap.ps1
.\scripts\run-dev.ps1
```

Open `http://localhost:5173`.

## Build

```powershell
.\scripts\build.ps1
```

## Hosted source and deployment transport

- `hosted/index.html`: normal, self-contained HTML/CSS/JavaScript source
- `hosted/vercel-transport/`: reproducible gzip/base64 chunk transport used to work around connector payload limits
- Production Vercel loader: pinned to immutable Git commit `a66a7cbf5f6bb9748c0356b1f32f962fc463856a`

## Validation boundary

The hosted v5 source passed HTML parsing, JavaScript syntax checking, JSON validation, local compression/decompression identity verification, Vercel production deployment, production-response verification, immutable first/last chunk retrieval, and runtime-error inspection. This execution environment does not contain the .NET SDK, and its npm mirror lacks required React type packages, so the definitive ASP.NET build, xUnit run, and dependency-backed Vite build must be completed on a development machine.
