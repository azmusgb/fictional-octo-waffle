# Workbench Studio v7 deployment

The hosted application is deployed through a Safari-compatible loader that fetches the complete `hosted/index.html` from an immutable Git commit. It does not use browser base64 decoding, gzip decompression, or `DecompressionStream`.

Production identifiers and the immutable source commit are recorded after deployment.
