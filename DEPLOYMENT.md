# Workbench Studio v8.4 rich-experience deployment

## Production

- URL: https://workbench-studio-v8.vercel.app
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`
- Production deployment ID: `dpl_GVEGfk2QqQPdWnuKiNycpoRUejac`
- Immutable application source commit: `8dc6a943cbcd845880885357fc687c7c22e7bb18`
- Production loader commit: `1af35920894a39ecef8ed8627bef8507c9c975b5`
- Region: `iad1`

## Delivery model

The production project serves a small Safari-compatible loader. It fetches the complete `hosted/index.html` from the immutable application source commit above, validates the Workbench Studio v8.4 identity, and replaces the loading document.

The loader includes `viewport-fit=cover`, Apple standalone-app metadata, iPhone safe-area padding, `Cache-Control: no-store, max-age=0`, and `X-Content-Type-Options: nosniff`. It does not use browser base64 decoding, gzip decompression, `atob`, or `DecompressionStream`.

## V8.4 scope

- Decision-posture hero with dynamic readiness, risk, coverage, review, snapshot, and active-profile context
- Contextual next actions for blockers, review, comparison, command search, and workspace tuning
- Layered evidence-network visual hierarchy and clearer active navigation
- Richer Portable Workspace summaries and descriptive progressive-disclosure tabs
- Responsive desktop, tablet, and phone composition
- Reduced-motion behavior and restrained interaction feedback
- Deterministic frontend installs and permanent exact-head CI
- Patched SQLite native bundle and clean .NET 10 restore, build, and test compatibility

Authoritative evidence, findings, policies, approvals, and audit records remain outside the preference and presentation layer.

## Verification

- Deployment state: `READY`
- Production alias returned HTTP 200
- Loader points to the immutable v8.4 application commit
- Mobile viewport and safe-area metadata are present
- Response cache policy is `no-store, max-age=0`
- `X-Content-Type-Options: nosniff` is present
- No compressed-browser transport is present
- Vercel runtime-error clusters after deployment: none detected
