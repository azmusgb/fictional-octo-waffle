# Workbench Studio v7

Workbench Studio is a local-first evidence operations, investigation, and decision-support workbench. It combines a React/TypeScript desktop-style shell, an ASP.NET Core local processing agent, SQLite metadata, disk-backed immutable artifacts, deterministic parsers, human review, continuous intake, quality profiling, impact analysis, privacy-safe exports, and transparent decision operations.

## Live hosted experience

Production: https://workbench-studio-v7.vercel.app

The hosted adapter uses representative browser-persisted data. Original evidence is processed only by the connected local agent.

## v7 product loop

`Connect → Watch → Snapshot → Profile → Validate → Trace → Prioritize → Compare to Baseline → Automate → Ask Evidence → Decide → Handoff`

## Signature v7 functionality

### Decision Cockpit

- Transparent artifact priority scores
- Explicit scoring factors for finding severity, privacy candidates, parser state, impact edges, and review status
- Critical, high, medium, and low priority bands
- Direct navigation from priority items to source artifacts
- Advisory scoring only; evidence and human approval remain authoritative

### Baseline Policy Center

- Approve any immutable snapshot as a baseline
- Generate default rules from accepted snapshot metrics
- Define inspectable `<=`, `>=`, and `==` thresholds
- Evaluate errors, warnings, parser failures, unsupported artifacts, inventory counts, privacy candidates, and other metrics
- Classify results as Passed, Improved, Regressed, or Needs Approval
- Persist complete per-rule outcomes and evaluation timestamps

### Automation Studio

- Compose reusable decision-readiness recipes
- Supported steps include profiling, privacy scan, lineage rebuild, baseline evaluation, and triage ranking
- Manual, hourly, daily, and on-snapshot triggers
- Background worker executes due recipes against the latest completed immutable snapshot
- Persisted progress, result summaries, failure state, enable/pause controls, and last-run timestamps

### Evidence Assistant

- Ask concrete questions of the selected snapshot
- Searches only local artifact metadata, bounded previews, and validation evidence
- Returns source-linked citations with artifact, finding, source location, excerpt, and basis
- Labels confidence from available supporting evidence
- Never represents an unsupported inference as an observed fact

### Handoff Brief Builder

- Generates a portable ZIP decision brief
- Includes snapshot identity, transparent triage results, findings, baseline policies, data profiles, and provenance
- Records generation time and safety notice
- Leaves original evidence unchanged

## Existing v6 operations retained

- Guided local-agent onboarding
- Watch folders and incremental immutable snapshots
- Data Quality Profiler
- Lineage and Impact Studio
- Investigation Playbooks
- Privacy and Redaction Center
- Evidence Workstation
- Import Control Room
- Caseboard
- Query Lab
- Rule Studio
- Review Queue
- Diff Studio
- Relationship Map
- Activity Timeline
- Saved views and exports

## Local architecture

- UI: React 19 + TypeScript + Vite
- Local agent: ASP.NET Core targeting .NET 10
- Metadata: SQLite
- Evidence storage: disk-backed originals, extraction cache, and exports
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

The script restores dependencies, runs backend and frontend tests, builds the Vite client, copies it into ASP.NET `wwwroot`, and publishes the combined local release.

## Safety boundary

- Imported content is never executed.
- ZIP extraction is path-bounded and resource-limited.
- XML DTD processing and external resolution are prohibited.
- Watch folders are copied into immutable staging; source folders are never modified.
- Sensitive-value detections are candidates requiring human review.
- Evidence Assistant answers remain citation-bound to the selected snapshot.
- Priority scores and baseline results are decision aids, not authoritative facts.
- Handoff briefs contain derived records and provenance; original evidence remains local unless explicitly shared.
