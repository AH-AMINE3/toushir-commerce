# TOUSHIR — full source handoff

The wholesale commerce back office and merchant ordering app built together are in the dedicated private repository **[AH-AMINE3/toushir-commerce](https://github.com/AH-AMINE3/toushir-commerce)** on **`main`**. A developer needs repository access through their own GitHub account.

## Two application projects and one shared backend

| Project | Source | Technology | Responsibility |
| --- | --- | --- | --- |
| Mobile ordering app | [`apps/mobile`](../apps/mobile) | React Native 0.88, Expo 58, TypeScript | Merchant catalogue, prices, basket, orders, delivery, offers, rewards and account. The default native identity is `MERCHANT`. |
| Back office | [`apps/web`](../apps/web) | React 19, TypeScript, Vite | Wholesaler orders, CRM, warehouse, dispatch, finance, campaigns and analytics. |
| Shared commerce API | [`backend`](../backend) | .NET 10, ABP 10.6.1, EF Core, OpenIddict, SignalR | Identity, permissions, business rules, persistence and live updates for both applications. |

The mobile project also supports the courier variant through `EXPO_PUBLIC_APP_ROLE=DRIVER`, with its own application identifier. Its shared components remain in the same mobile codebase. This does not combine the mobile project with the web back office: those are independent frontend projects, each with its own `package.json`, lockfile and build.

```text
apps/mobile/          React Native ordering app and courier variant
apps/web/             React back office
backend/              .NET / ABP API, business services and database migrations
catalog/              Product catalogue, provenance and 2,500 product images
docs/                 Architecture, API contracts, design and release boundaries
scripts/              Build, local launch and business verification
tests/browser/        Browser workflow tests
verification/         Recorded verification and design evidence
Dockerfile            Builds and packages both frontends with the API
compose.yaml          Application and persistent PostgreSQL for local hosting
```

## Clone and start all applications

Install Node.js 24, .NET SDK 10 and Git. Python 3 is used by additional verification scripts and the optional mobile preview server. Docker is optional for the PostgreSQL installation.

```sh
git clone https://github.com/AH-AMINE3/toushir-commerce.git
cd toushir-commerce
bash scripts/build-all.sh
bash scripts/run-local.sh
```

Open the back office at `http://localhost:5860/` first to create a fictional demo workspace. Its merchant and courier links share that workspace in the same browser. The launcher explicitly enables demo mode and preserves local SQLite data under `work/local-data`.

| View | URL |
| --- | --- |
| Both applications together | http://localhost:5860/live-work.html |
| Back office | http://localhost:5860/ |
| Merchant ordering app | http://localhost:5860/mobile/merchant |
| Courier app | http://localhost:5860/mobile/driver |
| API health | http://localhost:5860/api/health |

## Work on each frontend

Keep the API running with the local launcher above. In a second terminal, start the back-office development server:

```sh
cd apps/web
npm ci
npm run dev
```

Vite serves `http://127.0.0.1:5173` and proxies API/identity requests to port 5860. Rebuild the integrated installation with `bash scripts/build-all.sh` from the repository root.

For the mobile browser preview, use a separate terminal:

```sh
cd apps/mobile
npm ci
npm run build:web
npm run preview:web
```

Open `http://localhost:8086/mobile/merchant`. This serves the exported React Native web app with the same-origin API proxy. Re-export after changing source. Native development can use `npm run merchant`; native tools are optional and are not started by the repository's local launcher.

For a native device build, set `EXPO_PUBLIC_API_URL` to the reachable **API origin**, such as `https://your-host.example`. Omit `/api`: the client appends it. Leave the variable empty for the integrated browser deployment. Public Expo variables are compiled into the client and must contain no secrets. See [mobile setup](../apps/mobile/README.md) and [native distribution](NATIVE-DISTRIBUTION.md).

## Backend architecture and persistence

`backend/Toushir.Wholesale.csproj` is the .NET host. `ToushirCommerceModule.cs` initializes ABP, `Services/` owns business mutations, `Data/` owns models and the ABP database context, `Migrations/` contains SQLite and PostgreSQL migrations, and endpoint files expose authorized HTTP contracts. This is one modular application host; the implementation does not use a generated collection of separate ABP tier projects.

SQLite supports the local demo. The PostgreSQL provider is selected by the host's private `DATABASE_URL` configuration. For a durable Docker installation, follow [operations](../backend/OPERATIONS.md), copy the root `.env.example`, supply actual owner/database settings, then use `docker compose up --build -d`. Database contents, runtime identity keys, production credentials and local dependency/build folders are excluded from Git. The developer receives the schema, migrations, source assets and setup examples, rather than a copy of live credentials or business records.

## Verification and design scope

```sh
npm --prefix apps/web run typecheck
npm --prefix apps/mobile run typecheck
dotnet test backend/tests/Toushir.Domain.Tests.csproj --configuration Release
```

The [GitHub workflow](../.github/workflows/ci.yml) also builds both apps and the API, runs persistent business scenarios and checks the packaged browser interfaces. See [verification](VERIFICATION.md) for commands and recorded evidence. Run mutation suites against an isolated local demo, because they create and change test records.

The [Astra revision](ASTRA-DESIGN.md) documents the revised six core surfaces and motion implementation. The [41-state map](../verification/astra-design/STATE-COVERAGE.md) distinguishes directly implemented views, conditional states and consolidated equivalents. Full one-to-one fidelity across all 265 Figma states and native-device acceptance are not established. Existing downloadable native packages predate the newest browser design; rebuild from this source for an updated native package. External messaging, banking, fiscal invoicing and carrier integrations require provider configuration and separate validation.

## Hosted preview and access

- [Connected public preview](https://toushir-commerce-staging.onrender.com/live-work.html)
- [Merchant ordering app](https://toushir-commerce-staging.onrender.com/mobile/merchant)
- [Back office](https://toushir-commerce-staging.onrender.com/)
- [Figma Astra reference](https://www.figma.com/design/4IffhmSg2gHg82narIGfMF/TOUSHIR?node-id=267-2376)

These links open the demo; they do not grant GitHub repository access. The repository owner can invite the developer through GitHub repository settings. Render configuration and private signing/database material stay in their existing private stores. See [release boundaries](RELEASE.md) before production use.
