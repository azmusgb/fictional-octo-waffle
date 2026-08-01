# Workbench Studio v7 deployment

## Production

- URL: https://workbench-studio-v7.vercel.app
- Vercel project ID: `prj_d6z6OkcmLLmwc2UxJwfVcKRYdP7E`
- Production deployment ID: `dpl_BgE1FTn8UotZCCpwJDC6yTf8utWS`
- Immutable application source commit: `90c38d36cbe97e389dc79eeb4f9fe11ea92e44b4`
- Region: `iad1`

## Delivery model

The production project serves a small Safari-compatible loader. It fetches the complete `hosted/index.html` from the immutable source commit above, validates the Workbench Studio v7 identity, and replaces the loading document.

The loader does not use browser base64 decoding, gzip decompression, `atob`, or `DecompressionStream`. Responses use `Cache-Control: no-store, max-age=0` and `X-Content-Type-Options: nosniff`.

## Verification

- Deployment state: `READY`
- Production alias returned HTTP 200
- Loader points to the immutable v7 source commit
- No compressed-browser transport is present
- Vercel runtime-error clusters after deployment: none detected

The hosted application uses representative browser-persisted data until connected to the local ASP.NET Core agent. Real imports, watch-folder scans, SQLite persistence, decision policies, triggered automation, evidence questions, and decision-brief production remain local-agent responsibilities.
