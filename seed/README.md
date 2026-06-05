# MACH seed data

Idempotent TypeScript loaders that populate the demo's vendor sandboxes and the
local SQL fulfillment tables from a single shared catalog.

All scripts are **idempotent** (upsert by stable key / SKU / title) — safe to
re-run. They **type-check and lint without credentials**, and each exits with a
friendly message (non-zero) if its required env vars are missing, so nothing
ever calls a vendor API by accident.

## Layout

```
seed/
├─ catalog.json                       shared catalog (~25 products, 4 categories) — source for commercetools + Algolia
├─ lib/
│  ├─ env.ts                          .env loading (../.env), requireEnv(), runSeed() wrapper
│  └─ catalog.ts                      catalog model + validating loader
├─ commercetools/seed-commercetools.ts product type, categories, products, prices, inventory, discounts, shipping methods
├─ algolia/seed-algolia.ts            flatten catalog → records, index settings, query-suggestions index
├─ contentstack/seed-contentstack.ts  CMA content types + entries (PDP blocks, per-audience email templates) + publish
├─ sql/
│  ├─ fulfillment-seed.sql            MERGE upserts for Stores / Suppliers / ProductSuppliers
│  └─ seed-fulfillment.ts             runner that executes the .sql against SQL Server
└─ scripts/seed-all.ts               orchestrates the four in order
```

## Prerequisites

- Node 24 / npm 11.
- `npm install` inside `seed/`.
- For `seed:sql`: a reachable SQL Server (LocalDB `(localdb)\MSSQLLocalDB` or a
  mssql container) where the **Persistence/EF track has already created** the
  `fulfillment` schema and its tables. This seed inserts rows only; it does not
  create the schema.

## Configuration

Env vars are read from the repo-root `.env` (`e:\Personal\MACH\.env`, gitignored),
a `seed/.env` override, or the process environment. Copy `seed/.env.example` and
fill in the vars for the vendors you want to seed. Each script needs only its
own vendor's vars.

| Script | Required env |
|---|---|
| `seed:commercetools` | `CTP_PROJECT_KEY`, `CTP_CLIENT_ID`, `CTP_CLIENT_SECRET`, `CTP_AUTH_URL`, `CTP_API_URL` (opt: `CTP_SCOPES`) |
| `seed:algolia` | `ALGOLIA_APP_ID`, `ALGOLIA_ADMIN_API_KEY` (opt: `ALGOLIA_INDEX_NAME`, `ALGOLIA_SUGGESTIONS_INDEX_NAME`) |
| `seed:contentstack` | `CONTENTSTACK_API_KEY`, `CONTENTSTACK_MANAGEMENT_TOKEN`, `CONTENTSTACK_ENVIRONMENT` (opt: `CONTENTSTACK_CMA_BASE_URL`, `CONTENTSTACK_LOCALE`) |
| `seed:sql` | `SQL_CONNECTION_STRING` **or** (`SQL_SERVER` + `SQL_DATABASE`); opt login `SQL_USER`/`SQL_PASSWORD` else integrated auth |

## Run

Run order matters: **commercetools → Algolia → Contentstack → SQL**. commercetools
is the commerce source of truth; Algolia flattens the same `catalog.json`;
Contentstack keys PDP blocks by product slug; the SQL seed is independent but
runs last in the bundle.

```bash
npm install

# everything, in order:
npm run seed:all

# or individually:
npm run seed:commercetools
npm run seed:algolia
npm run seed:contentstack
npm run seed:sql
```

`seed:all` runs each step as its own process and prints a pass/fail summary; it
exits non-zero if any step failed or was skipped. Set
`SEED_CONTINUE_ON_ERROR=true` to push past a failing/unconfigured step (useful
when only some vendors have credentials).

## What gets seeded

**commercetools** — one `apparel` product type (attributes: size, color, brand,
material), 4 categories, ~25 published products each with a single-currency
price + master-variant SKU, an inventory entry per SKU (drives availability),
absolute product discounts on the on-sale items, and four shipping methods for
the delivery types **standard / express / same-day / store-pickup** (flat seed
prices; distance pricing is layered on at runtime by the BFF). A standard tax
category and a single delivery zone are created as supporting resources. Upsert
is by `key` (products/types/categories/shipping methods/discounts) and by `sku`
(inventory).

**Algolia** — each product flattened to a flat record (`objectID` = product
key): searchable `name`/`brand`/`category`/`description`/`color`/`material`;
facets on `brand`/`category`/`color`/`size`/`material`/`onSale`/`inStock` and a
`filterOnly(price)`; `customRanking` `desc(inStock), desc(popularity),
asc(priceWithDiscount)`. The main index is atomically reindexed
(`replaceAllObjects`) so it mirrors the catalog, and a **query-suggestions
index** is seeded from category/brand/product terms for autocomplete.

> The admin key is used here for indexing only. The storefront queries Algolia
> browser-side with a separate **search-only** key (canonical Algolia pattern).

**Contentstack** — content types **Home Hero, Promo Tile, Navigation, PDP
Marketing Block, Footer, Email Template** via the Management API. Entries: one
hero / promo / navigation / footer, a **PDP Marketing Block per product slug**,
and one **Email Template per audience** (`customer` / `store` / `supplier` /
`reception`) whose `subject`/`body` carry `{{order.*}}` / `{{delivery.*}}`
tokens. Entries are published to `CONTENTSTACK_ENVIRONMENT`. Content types are
upserted by `uid`; entries by `title`.

**SQL fulfillment** — five **Stores** (name, email, reception email, lat/lng
spread across German cities so delivery distances vary), three **Suppliers**,
and **ProductSuppliers** (SKU → supplier, by brand) into the `fulfillment`
schema via `MERGE` upserts keyed on natural keys. Column names align to
`Stores(Id, Name, Email, ReceptionEmail, Lat, Lng)`,
`Suppliers(Id, Name, Email)`, `ProductSuppliers(Sku, SupplierId)`.

## Verify (no vendor calls)

```bash
npm install
npm run build   # tsc --noEmit
npm run lint    # eslint .
```

Both pass without credentials. Running any `seed:*` without the required env
prints a friendly "configuration incomplete" message and exits 1 — it never
reaches a vendor.
