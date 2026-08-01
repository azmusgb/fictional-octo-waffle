# Workbench Studio evolution

## v6 product shift

Version 5 organized investigations after evidence was imported. Version 6 adds the operational loop that precedes and follows investigation:

`Connect → Watch → Snapshot → Profile → Validate → Trace Impact → Investigate → Redact → Export → Repeat`

## Design principles

- **Continuous, not noisy:** watches create snapshots only when fingerprints change.
- **Immutable intake:** watched source folders are never changed; each detected state becomes a separate import.
- **Profile before inspection:** users see dataset-level quality and drift before navigating individual records.
- **Explain impact:** changes are connected to rules, findings, cases, and reports through explicit lineage edges.
- **Repeatable work:** playbooks record deterministic steps and outcomes.
- **Share safely:** sensitive values are detected locally and removed only from derivative exports.
- **Evidence remains authoritative:** scores, profiles, and impact summaries remain navigational aids linked to source artifacts.
