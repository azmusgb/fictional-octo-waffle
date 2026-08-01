# Workbench Studio v8.3 portable-workspace deployment

## Production target

- Existing URL: https://workbench-studio-v8.vercel.app
- Existing Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`
- Target release: Workbench Studio v8.3 Portable Workspace
- Source state: validated release candidate pending immutable GitHub merge

## Delivery model

The production project serves a small Safari-compatible loader. After the feature merge, the loader will be pinned to the immutable v8.3 merge commit containing `hosted/index.html`. Browser gzip/base64 decoding, `atob`, and `DecompressionStream` remain absent.

## Scope

- Named workspace profiles
- Guided role onboarding
- Checksum-protected export, share, clipboard transfer, selective import, and preview
- Preference schema migration, backup, diagnostics, category reset, and recovery
- Explicit provider boundary for continuous synchronization

Final deployment identifiers and runtime verification will be recorded after production deployment.
