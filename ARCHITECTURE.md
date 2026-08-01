# Workbench Studio v6 architecture

## Core invariant

Every derived profile, privacy detection, lineage edge, playbook result, finding, review decision, comparison result, and export must remain traceable to an immutable import snapshot and its stored artifact identity.

## Runtime topology

```text
Hosted or local React shell
        │ HTTP to approved local origin
        ▼
ASP.NET Core local agent
        ├── project/import/artifact APIs
        ├── safe ZIP extraction + parser registry
        ├── import queue + worker
        ├── watch-folder scheduler
        ├── data profile service
        ├── lineage/impact service
        ├── playbook orchestration service
        ├── privacy scan + redacted export service
        ├── comparison/search/report services
        │
        ├── SQLite metadata
        └── disk-backed originals, extracts, caches, exports
```

## Persisted v6 records

- `WatchFolders`: local path, schedule, ignore patterns, fingerprint, scan state, last snapshot
- `DataProfiles`: artifact metrics and quality issues
- `LineageEdges`: source/target relationship, edge type, label, evidence
- `PrivacyDetections`: kind, severity, source location, masked preview, review status
- `Playbooks`: ordered step JSON, run status, progress, summary, timestamps

## Watch scan lifecycle

1. Resolve and validate the configured local folder.
2. Enumerate files after ignore-pattern filtering.
3. Build a deterministic metadata fingerprint from normalized path, size, and modified time.
4. Stop when the fingerprint is unchanged unless the user forces a scan.
5. Safely stage an archive copy in a new import workspace.
6. Persist an immutable queued import.
7. Submit the import to the existing parser queue.
8. Retain the watch folder's new fingerprint and import identifier.

## Profiling boundary

Profiles are deterministic and bounded. They are not statistical guarantees. Large text artifacts are sampled, XLSX profiles reuse the package parser's bounded structure summary, and parser errors become explicit profile issues.

## Privacy boundary

Privacy detection uses local deterministic patterns. Matches are candidates requiring review, not legal or compliance determinations. Redacted ZIPs contain only supported text artifacts and do not modify originals.

## Persistence compatibility

The current bootstrapper uses `CREATE TABLE IF NOT EXISTS` for additive v6 tables so existing local v5 workspaces can open without data loss. A formal migration chain remains recommended before broad production distribution.
