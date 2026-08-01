# Hosted deployment

## Production

- URL: https://workbench-studio-v5.vercel.app
- Vercel project ID: `prj_lpczTK9jhAZpg6ic1iKO2HhYThD4`
- Current deployment ID: `dpl_3eC1ARfkyBFfds8seYGdpdtZuBRS`
- Verified application source commit: `99104bf2f03d9e54a5ba352d5deafd2cb9356082`
- Loader compatibility fix commit: `484da45ef6a835d8ad301486d80eed37bb006c84`

## Transport model

The production loader retrieves the complete, verified `hosted/index.html` source directly from the pinned Git commit and replaces the loader document with that application HTML.

The earlier deployment used gzip-compressed, base64-encoded chunks and the browser `DecompressionStream` API. That implementation produced `Failed to Decode Data` on some Safari/iOS environments. The current loader removes base64 decoding, gzip decompression, and `DecompressionStream` entirely.

The loader response uses `Cache-Control: no-store` so browser refreshes receive the current production fix. This transport affects only hosted delivery; it does not change the local-first application or data boundary.
