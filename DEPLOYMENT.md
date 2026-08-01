# Workbench Studio v8.4 rich-experience deployment

## Production target

- Existing URL: https://workbench-studio-v8.vercel.app
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`
- Target release: Workbench Studio v8.4 Rich Experience
- Source state: validated release candidate pending immutable GitHub merge

## Delivery model

The production project serves a small Safari-compatible loader. After merge, the loader will be pinned to the immutable v8.4 application commit containing `hosted/index.html`. The loader remains cache-disabled and does not use browser base64 or decompression transport.

## Scope

- Decision-posture hero and contextual next actions
- Layered evidence-network visual hierarchy
- Stronger active navigation, surface feedback, and progressive disclosure
- Richer Portable Workspace summaries and descriptive tabs
- Responsive desktop, tablet, and phone composition
- Reduced-motion support and preserved local-first governance boundaries

Final deployment identifiers and runtime verification will be recorded after production deployment.
