# Validation record

## Hosted v5 source

- HTML parsed successfully.
- Embedded JavaScript passed `node --check`.
- JSON configuration parsed successfully.
- Required pages detected: Overview, Evidence Workstation, Review, Findings, Compare, Relationships, Activity, Saved Views, Caseboard, Query Lab, Rule Studio, and System Center.
- Required interaction symbols detected: `openTour`, `toggleFocus`, and `undoLast`.
- Original hosted HTML: 101,244 bytes.
- Gzip/base64 transport payload: 36,812 characters.
- Chunk count: 13.
- Local transport reconstruction is byte-identical to `hosted/index.html`.

## Connected delivery verification

- Immutable chunk commit: `a66a7cbf5f6bb9748c0356b1f32f962fc463856a`.
- First and final chunks retrieved successfully through the GitHub connector at the pinned commit.
- Vercel project: `workbench-studio-v5`.
- Vercel project ID: `prj_lpczTK9jhAZpg6ic1iKO2HhYThD4`.
- Production deployment: `dpl_FVkM9RKvzPWNGwLv8dwEtAjtyPje`.
- Production state: `READY`.
- Production alias: https://workbench-studio-v5.vercel.app
- Production response: HTTP 200.
- Vercel runtime error clusters after deployment: none detected.

## Environmental limitations

- The .NET SDK is not installed in this execution environment.
- The internal npm registry does not provide the required React type packages.
- The container cannot resolve public DNS, and its headless Chromium process cannot complete due missing system D-Bus services. Browser-level execution was therefore not claimed from the container.
- Backend compilation, xUnit execution, the normal Vite bundle, and final end-to-end tests must be run on the target development machine.
