# Workbench Studio v8.2 customization deployment

## Production

- URL: https://workbench-studio-v8.vercel.app
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`
- Production deployment ID: `dpl_EUgcRNfDSkYGzSSyxKDzZjjTLGkB`
- Immutable application source commit: `944e95f6065495eeebbbbb066603e975dc8c2bdd`
- Region: `iad1`

## Delivery model

The production project serves a small Safari-compatible loader. It fetches the complete `hosted/index.html` from the immutable source commit above, validates the Workbench Studio v8.2 identity, and replaces the loading document.

The loader includes `viewport-fit=cover`, Apple standalone-app metadata, and iPhone safe-area padding. It does not use browser base64 decoding, gzip decompression, `atob`, or `DecompressionStream`. Responses use `Cache-Control: no-store, max-age=0` and `X-Content-Type-Options: nosniff`.

## Verification

- Deployment state: `READY`
- Production alias returned HTTP 200
- Loader points to the immutable v8.2 source commit
- Mobile viewport and safe-area metadata are present
- No compressed-browser transport is present
- Vercel runtime-error clusters after deployment: none detected

The hosted application uses representative browser-persisted data. User preferences remain browser-local and separate from authoritative evidence, findings, policies, approvals, and audit records. Real evidence processing and governed persistence remain local-agent responsibilities.
