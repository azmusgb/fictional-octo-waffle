# Hosted deployment

## Production

- URL: https://workbench-studio-v5.vercel.app
- Vercel project ID: `prj_lpczTK9jhAZpg6ic1iKO2HhYThD4`
- Deployment ID: `dpl_FVkM9RKvzPWNGwLv8dwEtAjtyPje`
- Payload commit: `a66a7cbf5f6bb9748c0356b1f32f962fc463856a`

## Transport model

The complete `hosted/index.html` file is gzip-compressed, base64-encoded, and split into 13 immutable text chunks. The Vercel-hosted loader retrieves those chunks from the pinned commit, reconstructs the compressed stream, decompresses it with the browser `DecompressionStream` API, and replaces the loader document with the complete v5 application.

This transport exists only because the connected deployment interface limits large inline payloads. It does not change the application architecture or the local-first data boundary.
