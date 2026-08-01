# Workbench Studio v8.3 portable-workspace deployment

## Production

- URL: https://workbench-studio-v8.vercel.app
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`
- Production deployment ID: `dpl_B2L627YDMhMmL8qCBHvTpRNjcrjb`
- Immutable application source commit: `0d3590b7934846aee298ef545c4d34da6f6418d0`
- Production loader commit: `bcec47329122d1dd6075738e881f8d1ddf67356b`
- Region: `iad1`

## Delivery model

The production project serves a small Safari-compatible loader. It fetches the complete `hosted/index.html` from the immutable application source commit above, validates the Workbench Studio v8.3 identity, and replaces the loading document.

The loader includes `viewport-fit=cover`, Apple standalone-app metadata, and iPhone safe-area padding. It does not use browser base64 decoding, gzip decompression, `atob`, or `DecompressionStream`. Responses use `Cache-Control: no-store, max-age=0` and `X-Content-Type-Options: nosniff`.

## V8.3 scope

- Named built-in and custom workspace profiles
- Guided role onboarding
- Checksum-protected export, Web Share, download, and clipboard transfer
- Selective import preview and category-level apply
- Preference schema migration and automatic last-known-good backup
- Diagnostics export, category reset, onboarding restart, and recovery
- Explicitly disabled continuous cloud synchronization until authenticated storage exists

Preference packages contain user-owned presentation and workflow settings only. Original evidence, findings, policies, approvals, and audit records are excluded.

## Verification

- Deployment state: `READY`
- Production alias returned HTTP 200
- Loader points to the immutable v8.3 application commit
- Mobile viewport and safe-area metadata are present
- Response cache policy is `no-store, max-age=0`
- No compressed-browser transport is present
- Vercel runtime-error clusters after deployment: none detected
