FROM node:24-bookworm-slim AS desktop
WORKDIR /src/apps/web
COPY apps/web/package*.json ./
RUN npm ci --no-audit --no-fund
COPY apps/web/ ./
RUN npm run build

FROM node:24-bookworm-slim AS mobile
WORKDIR /src/apps/mobile
COPY apps/mobile/package*.json ./
RUN npm ci --no-audit --no-fund
COPY apps/mobile/ ./
ENV EXPO_NO_TELEMETRY=1 CI=1
RUN npx expo export --platform web

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY backend/ backend/
RUN dotnet restore backend/Toushir.Wholesale.csproj --locked-mode
RUN dotnet publish backend/Toushir.Wholesale.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=server /out/ ./
COPY --from=desktop /src/apps/web/dist/ ./wwwroot/
COPY --from=mobile /src/apps/mobile/dist/ ./wwwroot/mobile/
COPY catalog/products.json catalog/README.md catalog/featured-barcodes.json ./catalog/
COPY catalog/images/ ./catalog/images/
RUN mkdir -p /data && chown -R app:app /data
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 ASPNETCORE_ENVIRONMENT=Production DataPath=/data Catalog__Path=/app/catalog/products.json Catalog__Directory=/app/catalog Demo__Enabled=false
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet","Toushir.Wholesale.dll"]
