# Workbench Studio v8 validation record

## Completed before repository release

- Hosted HTML parsed successfully.
- Hosted embedded JavaScript passed `node --check`.
- Hosted page and runtime-navigation parity validated across 29 workbench surfaces.
- Adaptive Queue, Scenario Lab, Approval Gates, Explainability Studio, and Executive Briefing were detected.
- React source passed strict TypeScript checking with local compatibility declarations.
- JSON configuration files parsed successfully.
- Required frontend and backend v8 integration symbols were detected.
- Package manifest was regenerated from the release tree.

## Environment limits

- The .NET SDK is not installed in this execution environment, so ASP.NET compilation and xUnit execution must run through `scripts/build.ps1` on a development machine.
- The internal npm registry does not provide the required React packages, so the dependency-backed Vite build and Vitest must run on the development machine.
