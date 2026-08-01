# Workbench Studio v6 deployment

The hosted application is deployed as a small compatibility loader that fetches the complete `hosted/index.html` from an immutable Git commit. The loader does not use browser base64 decoding, gzip decompression, or `DecompressionStream`.

Production identifiers and the immutable source commit are recorded after deployment.
