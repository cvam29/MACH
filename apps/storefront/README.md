# MACH Storefront

The headless **Next.js (App Router, TypeScript, Tailwind v4, shadcn/ui)** storefront
for the MACH composable-commerce demo. This is the **Wave 1 shell**: chrome,
design system, providers, a typed Backend-for-Frontend (BFF) client and
auth/cart scaffolding. Real page logic (Algolia search, PDP merge, Adyen
checkout) lands in Wave 2 — placeholder pages render accessible skeletons today.

## Stack

- **Next.js 16** App Router (RSC-first), **React 19.2**, **TypeScript 5**
- **Tailwind CSS v4** (CSS-first config) + **shadcn/ui** (new-york, neutral)
- **TanStack Query** for client data/cache
- **Zustand** cart store mirroring the commercetools cart
- **zod** for response validation on every BFF/Auth call
- **Application Insights** web + React plugin (no-op without a connection string)
- Algolia (`algoliasearch` / `react-instantsearch` / autocomplete) and Adyen
  (`@adyen/adyen-web`) installed, wired in Wave 2

## Architecture notes

- **Typed BFF client** — `src/lib/bff/client.ts` wraps `fetch`, prefixes
  `NEXT_PUBLIC_BFF_URL`, sends cookies (`credentials: 'include'`), injects and
  propagates `traceparent` + `x-correlation-id`, and zod-validates responses.
  One method per route in the plan's API surface (catalog / search / products /
  carts / delivery-options / checkout / orders / content / health).
- **Auth client** — `src/lib/auth/client.ts` targets the Auth microservice
  (`NEXT_PUBLIC_AUTH_URL`): login / register / refresh / anonymous / logout / me.
  Tokens stay in httpOnly cookies; the browser only sees safe session JSON.
- **Providers** (`src/components/providers`) — `QueryClientProvider`,
  `AuthProvider` (reads `/auth/me`, exposes guest vs signed-in), `CartProvider`
  (Zustand store in `src/lib/cart/store.ts` holding `{ cartId, version,
  lineItems, totals }` with optimistic updates), and a telemetry provider. The
  root `layout.tsx` stays a Server Component and reads the session server-side.
- **Account guard** — `/account` redirects to `/login?next=/account` when there
  is no signed-in customer (server-side check in `src/lib/server/session.ts`,
  resilient to the Auth host being offline).

## Routes

`/` · `/search` · `/catalog/[category]` · `/product/[slug]` · `/login` ·
`/register` · `/account` (protected) · `/cart` · `/checkout` · `/order/[id]` ·
`/api/health`.

## Getting started

```bash
npm install
cp .env.example .env.local   # fill in vendor keys as needed
npm run dev                  # http://localhost:3000
```

The shell renders fully **offline**: BFF/Auth calls fail soft to guest/empty
state, and telemetry is disabled without a connection string. Point
`NEXT_PUBLIC_BFF_URL` / `NEXT_PUBLIC_AUTH_URL` at the local .NET hosts
(`func start`) once they are running.

## Scripts

- `npm run dev` — dev server (Turbopack)
- `npm run build` — production build
- `npm run start` — serve the production build
- `npm run lint` — ESLint (flat config)
- `npx tsc --noEmit` — type-check

## Environment

See [`.env.example`](./.env.example). Only `NEXT_PUBLIC_*` values reach the
browser; all real secrets live in the .NET hosts / Key Vault.
