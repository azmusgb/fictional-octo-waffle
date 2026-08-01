# Workbench Studio v8.2

Workbench Studio is a local-first evidence operations, investigation, decision-support, and command-intelligence workbench. It combines a React/TypeScript desktop shell, an ASP.NET Core local agent, SQLite metadata, immutable disk-backed evidence, deterministic parsers, human review, continuous intake, quality profiling, impact analysis, transparent decision operations, and accountable executive command workflows.

## Hosted experience

Production: https://workbench-studio-v8.vercel.app

The hosted adapter uses representative browser-persisted data. Original evidence remains local unless the user explicitly exports or shares it.


## Personal workspace v8.2

Version 8.2 adds a user-owned customization layer that remains strictly separate from authoritative evidence and decision records:

- Configurable phone bottom navigation with up to four ordered destinations
- Show, hide, and reorder controls for the overview dashboard
- Standard, Large Touch, High Contrast, Dense Review, and Evidence Reading display profiles
- Device-following light/dark mode, text scaling, reduced motion, left-handed controls, and approved accent choices
- Personal start workspace, default findings severity, queue sorting, and alert-delivery preferences
- Up to four safe mobile quick actions
- Versioned browser persistence and one-action reset to recommended defaults

Customization never changes source files, hashes, findings, baseline outcomes, queue calculations, approval controls, or audit records.

## Mobile-first v8.1 experience

The v8.1 shell is optimized for phones without removing desktop functionality:

- Four persistent mobile tabs plus an all-workspaces navigation sheet
- Safe-area-aware top and bottom chrome for modern iPhones
- Full-screen artifact inspector and slide-in evidence tree
- Touch-friendly review and query cards
- Scrollable workspace tabs and compact command controls
- Bottom-sheet dialogs and command palette
- Responsive command queue, scenarios, approvals, explainability, and executive briefing

The hosted experience remains representative. Real evidence, persistence, approvals, and exports remain local-agent responsibilities.

## v8 product loop

`Connect → Watch → Snapshot → Profile → Validate → Trace → Prioritize → Simulate → Gate → Explain → Approve → Brief → Handoff`

## Signature v8 functionality

### Adaptive Command Queue

- Persists transparent queue policies and factor multipliers
- Re-ranks evidence using risk, review state, impact, and SLA pressure
- Shows rank, score, band, due time, SLA state, and every contributing reason
- Supports one active queue policy per project

### Scenario Lab

- Runs non-destructive remediation simulations
- Projects readiness after explicit metric adjustments
- Preserves the current and projected metric values, score delta, status, and recommendations
- Never changes findings, reviews, baselines, approvals, or original evidence

### Approval Gates

- Creates formal release or distribution controls for immutable snapshots
- Evaluates deterministic requirements for errors, privacy candidates, parser failures, and baseline regressions
- Blocks approval while required controls fail
- Persists reviewer identity, rationale, status, and decision time

### Explainability Studio

- Groups anomalies by artifact and source evidence
- Separates observed behavior, expected state, causal drivers, impact, and corrective action
- Keeps every explanation linked to findings, rule IDs, source locations, and excerpts

### Executive Briefing

- Produces a readiness score and explicit decision status
- Summarizes critical queue items, approval gates, baseline regressions, privacy posture, and top leadership priorities
- Generates a ZIP containing HTML and JSON executive records

## Existing v7 capabilities retained

- Decision Cockpit, baselines, automation, Evidence Assistant, and decision handoff
- Agent onboarding, watch folders, data profiling, lineage, playbooks, and privacy-safe exports
- Evidence Workstation, import control room, Caseboard, Query Lab, Rule Studio, review queue, Diff Studio, relationship map, activity timeline, saved views, and exports

## Local architecture

- UI: React 19 + TypeScript + Vite
- Local agent: ASP.NET Core targeting .NET 10
- Metadata: SQLite
- Evidence storage: disk-backed immutable originals, extraction cache, and exports
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

## Safety boundary

- Imported content is never executed.
- Queue scores, scenarios, explanations, and executive readiness are advisory derivatives.
- Scenario simulation never mutates source evidence or authoritative review records.
- Approval requires every persisted deterministic requirement to pass.
- Sensitive-value detections remain candidates requiring human review.
- Executive briefs contain derived records and provenance, not original evidence bytes by default.
