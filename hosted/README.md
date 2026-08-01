# Workbench Studio v7 hosted experience

`index.html` is a dependency-free, self-contained demonstration of the complete Workbench Studio v7 workflow. It includes 24 product surfaces and persists demo decisions in browser `localStorage`.

The hosted adapter does not process real evidence unless the user connects the local ASP.NET Core agent. The local agent remains authoritative for immutable imports, SQLite records, watch folders, profiles, lineage, privacy detections, baseline policies, automation recipes, citation-first evidence answers, and decision briefs.

Production uses the Safari-compatible loader in `vercel-transport/`. The loader fetches the complete hosted source from an immutable Git commit and does not use `DecompressionStream`.
