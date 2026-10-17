#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
(cd apps/web && npm ci --no-fund --no-audit && npm run build)
(cd apps/mobile && npm ci --no-fund --no-audit && EXPO_NO_TELEMETRY=1 CI=1 npx expo export --platform web)
node scripts/finalize-apps.mjs
dotnet restore backend/Toushir.Wholesale.csproj --locked-mode
dotnet build backend/Toushir.Wholesale.csproj --no-restore
