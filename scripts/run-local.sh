#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [ ! -f backend/wwwroot/index.html ] || [ ! -f backend/wwwroot/mobile/index.html ]; then bash scripts/build-all.sh; fi
export ASPNETCORE_ENVIRONMENT=Development
export Demo__Enabled=true
export DataPath="${TOUSHIR_DATA_PATH:-$PWD/work/local-data}"
export Catalog__Path="$PWD/catalog/products.json"
export Catalog__Directory="$PWD/catalog"
printf 'TOUSHIR platform: http://localhost:5860\nMerchant app: http://localhost:5860/mobile/merchant\nDriver app: http://localhost:5860/mobile/driver\n'
exec dotnet run --project backend/Toushir.Wholesale.csproj --no-launch-profile --urls http://0.0.0.0:5860
