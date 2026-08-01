# Workbench Studio v8.1 mobile deployment

## Production

- URL: https://workbench-studio-v8.vercel.app
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`
- Production deployment ID: `dpl_95mkTwhKCVJ9VPnVKWr8s8QSWKE2`
- Immutable application source commit: `0151b03cce091ab320acaa4ee0c5a6984b4192de`
- Region: `iad1`

## Delivery model

The existing v8 production project serves a small Safari-compatible loader. It fetches the complete `hosted/index.html` from the immutable v8.1 source commit above, validates the Workbench Studio v8.1 identity, and replaces the loading document.

The loader includes `viewport-fit=cover`, Apple standalone-app metadata, and iPhone safe-area padding. It does not use browser base64 decoding, gzip decompression, `atob`, or `DecompressionStream`. Responses use `Cache-Control: no-store, max-age=0` and `X-Content-Type-Options: nosniff`.

## Verification

- Deployment state: `READY`
- Existing production alias returned HTTP 200
- Loader points to the immutable v8.1 source commit
- Mobile viewport and safe-area metadata are present
- No compressed-browser transport is present
- Vercel runtime-error clusters after deployment: none detected

The hosted application uses representative browser-persisted data until connected to the local ASP.NET Core agent. Real evidence processing, persistence, approvals, scenarios, and exports remain local-agent responsibilities.
