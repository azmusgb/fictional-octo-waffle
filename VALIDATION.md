# Workbench Studio v8 validation record

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

- Source pull request: `#6`.
- Immutable application source commit: `f9d63a0cb83a97537dd09171170f0b3986ffd980`.
- Vercel project: `workbench-studio-v8`.
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`.
- Production deployment: `dpl_6LM9RxirhwEkqiW4SmftxYTVCb2V`.
- Production state: `READY`.
- Production alias: https://workbench-studio-v8.vercel.app
- Production response: HTTP 200.
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
