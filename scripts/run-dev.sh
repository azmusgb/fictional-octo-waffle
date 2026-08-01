#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ ! -d "$ROOT/client/node_modules" ]]; then
  dotnet restore "$ROOT/WorkbenchStudio.sln"
  (cd "$ROOT/client" && npm install)
fi

dotnet run --project "$ROOT/server/WorkbenchStudio.Api/WorkbenchStudio.Api.csproj" &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true' EXIT
(cd "$ROOT/client" && npm run dev)
