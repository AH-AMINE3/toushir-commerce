# TOUSHIR back office

Independent React / TypeScript / Vite frontend for the wholesaler, order operations, CRM, warehouse, dispatch, finance and campaigns. The shared .NET 10 / ABP backend lives in `../../backend`; the React Native ordering app lives in `../mobile`.

Start with the repository's [developer handoff](../../docs/DEVELOPER-HANDOFF.md). Use Node.js 24 and keep the local API running on port 5860.

```sh
npm ci
npm run dev
```

Open `http://127.0.0.1:5173`. The Vite proxy forwards API and identity traffic to the local backend. `npm run typecheck` checks TypeScript; `npm run build` creates `dist/`.

The integrated build serves this frontend at `/` and the mobile export at `/mobile`, both through the same API host. Run `bash scripts/build-all.sh` and `bash scripts/run-local.sh` from the repository root for that installation. Business mutations and authority remain in backend services.
