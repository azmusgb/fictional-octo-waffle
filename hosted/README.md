# Hosted v5 adapter

`index.html` is the complete dependency-free hosted experience. It runs in demo mode and stores cases, evidence pins, hypotheses, tasks, queries, validation rules, review decisions, saved views, activity, tour state, focus mode, theme, density, and connection preference in browser `localStorage`.

Production: https://workbench-studio-v5.vercel.app

`vercel-transport/` contains the reproducible chunked gzip/base64 representation used to deploy the source through a connector with inline payload limits. The normal development artifact remains `index.html`.
