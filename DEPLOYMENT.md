# Workbench Studio v6 deployment

## Production

- URL: https://workbench-studio-v6.vercel.app
- Vercel project ID: `prj_QV3KdfS3TFVRfZ5o4vgX7vsBEVI8`
- Production deployment ID: `dpl_99XF4qmSMMjj5YDYk8Me6u4tV9f1`
- Immutable application source commit: `dd046974605d775733ed90bc38faffda304c6556`
- Region: `iad1`

## Delivery model

The hosted application is deployed as a small compatibility loader that fetches the complete `hosted/index.html` from the immutable Git commit above. The loader does not use browser base64 decoding, gzip decompression, or `DecompressionStream`.

The loader response uses `Cache-Control: no-store` and validates that the retrieved source identifies itself as Workbench Studio v6 before replacing the loading document.

## Verification

- Production deployment state: `READY`
- Production alias returned HTTP 200
- Loader points to the immutable v6 merge commit
- No browser decompression code is present
- Vercel runtime-error clusters after deployment: none detected

The hosted application uses representative browser-persisted data until connected to the local ASP.NET Core agent. Real imports, watch-folder scans, SQLite persistence, profiles, lineage, playbook runs, privacy detections, and redacted exports remain local-agent responsibilities.
