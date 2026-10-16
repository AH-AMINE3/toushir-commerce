FROM node:24-bookworm-slim AS web
WORKDIR /src/apps/mobile
COPY apps/mobile/package*.json ./
RUN npm ci --no-audit --no-fund
COPY apps/mobile/ ./
ENV EXPO_NO_TELEMETRY=1 CI=1
RUN npx expo export --platform web
COPY scripts/finalize-web.mjs /src/scripts/finalize-web.mjs
RUN node /src/scripts/finalize-web.mjs

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY backend/Toushir.Wholesale.csproj backend/
RUN dotnet restore backend/Toushir.Wholesale.csproj
COPY backend/ backend/
RUN dotnet publish backend/Toushir.Wholesale.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=server /out/ ./
COPY --from=web /src/apps/mobile/dist/ ./wwwroot/
COPY catalog/products.json ./catalog/products.json
COPY catalog/README.md ./catalog/README.md
COPY catalog/featured-barcodes.json ./catalog/featured-barcodes.json
COPY catalog/images/ ./catalog/images/
RUN mkdir -p /data && chown -R app:app /data
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 ASPNETCORE_ENVIRONMENT=Production DataPath=/data Catalog__Path=/app/catalog/products.json Catalog__Directory=/app/catalog Demo__Enabled=false
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Toushir.Wholesale.dll"]
