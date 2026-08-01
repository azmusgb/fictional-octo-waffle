# Workbench Studio v7 architecture

## Core invariant

Every derived profile, finding, privacy detection, lineage edge, priority factor, baseline result, automation result, evidence answer, review decision, and export must remain traceable to an immutable snapshot and its stored artifact identity.

## Runtime topology

```text
Hosted or local React shell
        │ HTTP to approved local origin
        ▼
ASP.NET Core local agent
        ├── immutable import queue and parsers
        ├── watch-folder scheduler
        ├── data profile service
        ├── lineage and impact service
        ├── privacy and redaction service
        ├── investigation playbooks
        ├── decision triage service
        ├── baseline evaluation service
        ├── automation recipe worker
        ├── citation-first evidence assistant
        └── portable decision brief builder
        │
        ├── SQLite metadata
        └── disk-backed originals, extracts, caches, and exports
```

## Decision Operations data

### BaselinePolicies

Stores the approved baseline snapshot, explicit metric rules, last evaluated snapshot, complete rule-level result JSON, status, and timestamps.

### AutomationRecipes

Stores visible ordered steps, trigger mode, schedule interval, enable state, progress, status, last-run summary, and timestamps.

### Triage

Triage is calculated on demand from persisted evidence. Each score is the sum of visible factors:

- Error, warning, and informational findings
- Open or confirmed privacy detections
- Lineage and impact edges
- Parser failure or unsupported status
- Human review state

Scores are capped at 100 and mapped to priority bands. They are advisory and are not persisted as authoritative conclusions.

### Evidence Assistant

The default assistant is deterministic and local. It tokenizes the user question, ranks matching findings and bounded artifact previews, and returns citations. It does not use external inference or upload evidence.

### Decision brief

A ZIP derivative contains JSON records for snapshot identity, triage, findings, baselines, and profiles plus a safety notice. Original evidence bytes are not duplicated into the brief by default.

## Automation trigger model

The background recipe worker checks once per minute but enforces the configured minimum cadence:

- `OnSnapshot`: latest completed snapshot is newer than the last run
- `Hourly`: at least configured interval, minimum 60 minutes
- `Daily`: at least configured interval, minimum 1,440 minutes
- `Manual`: never runs automatically

## Persistence compatibility

Additive v7 tables use `CREATE TABLE IF NOT EXISTS` so existing v6 workspaces can open without data loss. A formal migration chain remains required before broad production distribution.
