# Workbench Studio v8.4 validation record

## Completed in this environment

- Hosted HTML parsed successfully.
- Hosted embedded JavaScript passed `node --check`.
- Hosted page and runtime-navigation parity validated across 29 workbench surfaces.
- Adaptive Queue, Scenario Lab, Approval Gates, Explainability Studio, and Executive Briefing were detected.
- React source passed strict TypeScript checking with local compatibility declarations.
- JSON configuration files and MSBuild XML parsed successfully.
- Sample ZIP and XLSX fixtures passed archive-integrity checks.
- Required frontend and backend v8 integration symbols were detected.
- Additive SQLite schema for queue policies, scenario runs, and approval gates executed successfully in validation.
- All v8-modified C# files passed delimiter-aware lexical scanning.
- Package manifest was reconciled with the release tree.
- Source ZIP integrity and SHA-256 verification completed.

## Connected delivery verification

- Mobile source pull request: `#8`.
- Immutable mobile application source commit: `0151b03cce091ab320acaa4ee0c5a6984b4192de`.
- Vercel project: `workbench-studio-v8`.
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`.
- Production deployment: `dpl_95mkTwhKCVJ9VPnVKWr8s8QSWKE2`.
- Production state: `READY`.
- Production alias: https://workbench-studio-v8.vercel.app
- Production response: HTTP 200.
- Existing clean production alias retained.
- `viewport-fit=cover`, Apple web-app metadata, and safe-area loader padding confirmed.
- Response cache policy: `no-store, max-age=0`.
- Browser gzip/base64 decoding, `atob`, and `DecompressionStream`: absent.
- Runtime error clusters after deployment: none detected.

## Environmental limits

- The .NET SDK is not installed in this execution environment, so ASP.NET compilation and xUnit execution could not be performed here.
- The internal npm registry does not provide the required React packages, so the dependency-backed Vite build and Vitest could not be performed here.
- `scripts/build.ps1` remains the definitive restore, compile, test, bundle, and publish validation on the development machine.

## v8.1 mobile validation

- Hosted HTML parsed after mobile metadata, navigation, drawer, and card-table changes.
- Embedded hosted JavaScript passed `node --check`.
- React source passed strict TypeScript checking using local compatibility declarations.
- Mobile navigation exposes four primary tabs plus all 29 workspaces through the More sheet.
- Artifact tree and inspector remain accessible on screens at or below 760 pixels.
- iOS safe-area, 44-pixel touch-target, and 16-pixel form-control rules are present.
- Desktop navigation and local-agent APIs remain unchanged.

## v8.2 customization validation

- React customization source passed TypeScript syntax transpilation and strict checking with local React/Vitest compatibility declarations.
- Hosted HTML parsed successfully after customization overlay, dashboard, quick-action, and navigation changes.
- Hosted embedded JavaScript passed `node --check`.
- Preference persistence is versioned and stored separately from evidence, findings, baselines, approvals, and audit state.
- Mobile tabs are configurable and reorderable with a four-item maximum.
- Dashboard widgets support persistent visibility and ordering.
- Display profiles, device theme, text scale, contrast, motion, handedness, queue defaults, alerts, and safe quick actions are persisted.
- Package JSON and MSBuild XML remain valid.
- Dependency-backed Vite/Vitest and ASP.NET/xUnit remain development-machine release gates because dependencies and the .NET SDK are unavailable in this environment.

## v8.2 connected delivery verification

- Source pull request: `#9`.
- Immutable application source commit: `944e95f6065495eeebbbbb066603e975dc8c2bdd`.
- Vercel project: `workbench-studio-v8`.
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`.
- Production deployment: `dpl_EUgcRNfDSkYGzSSyxKDzZjjTLGkB`.
- Production state: `READY`.
- Production alias: https://workbench-studio-v8.vercel.app
- Production response: HTTP 200.
- Response cache policy: `no-store, max-age=0`.
- Loader source is pinned to the immutable v8.2 merge commit.
- Browser gzip/base64 decoding, `atob`, and `DecompressionStream`: absent.
- Runtime error clusters after deployment: none detected.

## v8.3 portable-workspace validation

- Preference schema migrated from version 1 to version 2 with legacy recovery.
- Named built-in and custom profiles support apply, save, update, and delete.
- Export packages are checksum-protected and exclude evidence and governed records.
- Import validates package identity and checksum and supports category-level preview and apply.
- Automatic preference backup, restore, category reset, diagnostics export, and onboarding restart are implemented.
- Guided role presets cover investigator, reviewer, approver, operations, and executive workflows.
- Hosted embedded JavaScript passed syntax validation after the portable-workspace additions.
- React preference and customization sources passed strict TypeScript checking with local compatibility declarations before materialization.
- Continuous cloud synchronization is explicitly disabled until an authenticated storage provider is available.

## v8.3 connected delivery verification

- Source pull request: `#10`.
- Corrected materialization payload SHA-256: `b400262946266e05b2ecdfc9d884aeac8fd74aab0bfc1c6d1c519b82988aea3d`.
- GitHub materialization workflow run: `30721985479`.
- Immutable application source commit: `0d3590b7934846aee298ef545c4d34da6f6418d0`.
- Production loader commit: `bcec47329122d1dd6075738e881f8d1ddf67356b`.
- Vercel project: `workbench-studio-v8`.
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`.
- Production deployment: `dpl_B2L627YDMhMmL8qCBHvTpRNjcrjb`.
- Production state: `READY`.
- Production alias: https://workbench-studio-v8.vercel.app
- Production response: HTTP 200.
- Response cache policy: `no-store, max-age=0`.
- Loader source is pinned to the immutable v8.3 merge commit.
- Browser gzip/base64 decoding, `atob`, and `DecompressionStream`: absent.
- Runtime error clusters after deployment: none detected.
## v8.4 rich-experience validation

- React source adds a decision-posture hero with dynamic health, open-risk, coverage, review, snapshot, and active-profile context.
- Direct next actions route to findings, review, compare, command search, and workspace customization without mutating evidence.
- Portable Workspace exposes profile, onboarding, transfer, and recovery state before progressive-detail tabs.
- Hosted and React surfaces share the same experience hierarchy and local-first trust language.
- Embedded hosted JavaScript passed `node --check`.
- React source passed strict TypeScript checking with local compatibility declarations.
- JSON package files parsed successfully.
- Mobile layouts collapse hero actions, control deck, summary cards, and portable navigation without horizontal dependency.
- Reduced-motion behavior disables ambient node animation and nonessential surface transitions.
- No evidence, parser, SQLite, policy, approval, or audit-record behavior was changed.

