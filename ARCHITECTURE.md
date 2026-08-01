# Workbench Studio v8 architecture

## Core invariant

Every profile, finding, privacy detection, lineage edge, queue factor, scenario projection, approval requirement, anomaly explanation, baseline result, automation result, evidence answer, review decision, and export must remain traceable to an immutable snapshot and stored artifact identity.

## Runtime topology

```text
Hosted or local React shell
        │ HTTP to approved local origin
        ▼
ASP.NET Core local agent
        ├── immutable import queue and deterministic parsers
        ├── watch-folder scheduler
        ├── profiles, lineage, privacy, and playbooks
        ├── decision triage, baselines, and automation
        ├── citation-first Evidence Assistant
        ├── adaptive command queue policies
        ├── non-destructive scenario simulation
        ├── approval-gate enforcement
        ├── anomaly explanation service
        └── decision and executive brief builders
        │
        ├── SQLite metadata
        └── disk-backed originals, extracts, caches, and exports
```

## Persisted v8 records

### QueuePolicies

Stores visible factor multipliers, SLA hours, active state, and timestamps. Queue ranks are calculated on demand from persisted evidence and the selected policy.

### ScenarioRuns

Stores the immutable source snapshot, explicit metric assumptions, current/projected readiness, metric deltas, status, recommendations, and execution time. A scenario never changes source records.

### ApprovalGates

Stores snapshot identity, gate type, required role, deterministic requirement evidence, status, reviewer, rationale, and decision timestamp. Approval is rejected by the API while any required control fails.

## Calculated v8 products

- Adaptive queue items combine triage factors, policy multipliers, and SLA state.
- Anomaly explanations group findings into observed, expected, driver, impact, evidence, and next-action sections.
- Executive summaries combine queue, approval, baseline, privacy, and snapshot metrics.
- Executive brief ZIPs contain HTML and JSON derivatives without copying original evidence by default.

## Persistence compatibility

V8 tables are additive and created with `CREATE TABLE IF NOT EXISTS`, allowing existing v7 workspaces to open without data deletion. A formal migration chain remains required before broad production distribution.
