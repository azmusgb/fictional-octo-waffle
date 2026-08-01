# Workbench Studio v8.2 customization deployment

## Production target

- Existing production URL: https://workbench-studio-v8.vercel.app
- Existing Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`
- Target release: Workbench Studio v8.2 Personal Workspace
- Source state: validated release candidate pending immutable GitHub merge

## Delivery model

The production project serves a small Safari-compatible loader. After the v8.2 feature merge, the loader will be pinned to the immutable merge commit containing `hosted/index.html`, will validate the Workbench Studio v8.2 identity, and will replace the loading document.

The loader does not use browser base64 decoding, gzip decompression, `atob`, or `DecompressionStream`. Responses remain cache-disabled.

## V8.2 scope

- Configurable mobile primary navigation
- Dashboard widget visibility and ordering
- Display and accessibility profiles
- Personal queue and alert preferences
- Safe configurable quick actions
- Versioned browser preference persistence isolated from authoritative evidence and decision data

Final deployment ID, immutable source commit, readiness status, and runtime verification will be recorded after production deployment.
