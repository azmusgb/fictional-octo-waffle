# Workbench Studio Architecture

## Core invariant

Every derived result must remain traceable to an immutable import snapshot, stored artifact path, SHA-256 content hash, parser identifier, parser version, and source evidence location.

## Runtime topology

```text
Browser UI (React + TypeScript)
        │ HTTP / multipart
        ▼
Local ASP.NET Core API
        ├── Import queue + background worker
        ├── Safe ZIP extraction
        ├── Parser registry
        │   ├── JSON
        │   ├── CSV
        │   ├── XML
        │   ├── Text / log
        │   └── XLSX package analysis
        ├── Validation findings
        ├── Search
        ├── Snapshot comparison
        └── Report generation
        │
        ├── SQLite metadata: .workspace/workbench.db
        └── File storage: .workspace/projects/{project}/imports/{snapshot}/...
```

## Storage model

SQLite stores project state, immutable snapshot metadata, artifact indexes, hashes, parser results, findings, processing state, and export history. Original and extracted files remain file-backed to avoid database bloat and permit range-enabled streaming.

## Import lifecycle

`Queued → Preparing → Extracting → Inventorying → Parsing → Validating → Indexing → Completed|CompletedWithWarnings`

Cancellation is persisted in SQLite and checked between extraction and parsing operations. Interrupted nonterminal imports return to `Queued` during API startup. Failed/cancelled imports may be retried only while staged originals remain available.

## Parser contract

A parser receives a file path, normalized relative path, extension, size, and cancellation token. It returns:

- Parse status
- Parser ID and semantic version
- Structured summary
- Bounded safe preview
- Findings with severity, rule ID, location, evidence, and recommendation
- Optional parser error

The parser registry selects the first parser that declares support for the artifact context.

## XLSX boundary

XLSX is treated as an Office Open XML ZIP package, not as executable spreadsheet content.

- Package entries and total expanded bytes are bounded.
- XML DTD processing and external resolution are prohibited.
- Workbook relationships resolve worksheet package paths.
- Shared strings and worksheet cells are read for inventory and bounded preview.
- Formula expressions are detected; cached values may be displayed.
- Formulas are never recalculated.
- Macros are never executed.
- Hidden-sheet presence is surfaced as evidence.

## Search boundary

The global command palette calls the local `/search` endpoint. Search operates over persisted metadata, paths, hashes, parser identities, rules, titles, and finding messages. It does not send content to external services.

## Workspace health heuristic

The client computes an advisory score from:

- Parse coverage
- Unsupported artifact ratio
- Parser failure ratio
- Error and warning counts

The score is a prioritization aid only. It is not persisted, audited, or represented as an authoritative validation outcome.

## Security controls

- ZIP paths are resolved under a fixed extraction root.
- Absolute paths and traversal outside the workspace are rejected.
- Limits cover upload size, per-file size, extracted bytes, extracted files, and compression ratio.
- XLSX package inspection adds entry-count and expanded-package limits.
- XML DTD processing and external resolution are disabled.
- Imported content is never executed.
- Raw artifact delivery uses stored paths rather than user-supplied filesystem paths.
- Download responses are range-enabled and retain the stored filename/media type.

## Current deliberate limits

- Inventory loading is capped at 2,000 rows per client request.
- Persistence initialization still uses `EnsureCreated`; committed migrations are required before long-lived production upgrades.
- Progress uses polling rather than WebSockets, SignalR, or server-sent events.
- `.xls`, PDF, Word, PowerPoint, and unknown binary content remain inventory-only.
- Authentication and multi-user collaboration are excluded from the local-first product boundary.
