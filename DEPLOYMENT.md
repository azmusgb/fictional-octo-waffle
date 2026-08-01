# Workbench Studio v8 deployment

## Production

- URL: https://workbench-studio-v8.vercel.app
- Vercel project ID: `prj_0gUJSBVDQs2xuydpQurcpVa7cL27`
- Production deployment ID: `dpl_6LM9RxirhwEkqiW4SmftxYTVCb2V`
- Immutable application source commit: `f9d63a0cb83a97537dd09171170f0b3986ffd980`
- Region: `iad1`

## Delivery model

The production project serves a small Safari-compatible loader. It fetches the complete `hosted/index.html` from the immutable source commit above, validates the Workbench Studio v8 identity, and replaces the loading document.

The loader does not use browser base64 decoding, gzip decompression, `atob`, or `DecompressionStream`. Responses use `Cache-Control: no-store, max-age=0` and `X-Content-Type-Options: nosniff`.

## Verification

- Deployment state: `READY`
- Production alias returned HTTP 200
- Loader points to the immutable v8 source commit
- No compressed-browser transport is present
- Vercel runtime-error clusters after deployment: none detected

The hosted application uses representative browser-persisted data until connected to the local ASP.NET Core agent. Real imports, queue policies, scenario runs, approval decisions, anomaly explanations, SQLite persistence, and executive brief production remain local-agent responsibilities.
