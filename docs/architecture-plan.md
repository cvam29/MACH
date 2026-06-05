# Plan: MACH Architecture Commerce Demo (Portfolio)

> **Living document.** The first execution task copies this plan to `e:\Personal\MACH\docs\architecture-plan.md` and it becomes the project's source-of-truth architecture doc (kept in sync as milestones land). All ADRs, diagrams, and guides referenced below live under `docs/`.
>
> **Execution style: subagent-driven with parallel waves.** A coordinator (the main session) dispatches a fresh subagent per task and runs a **two-stage review after each — spec compliance first, then code quality** — never proceeding with open issues. Independent tasks that touch **disjoint directory subtrees** run **in parallel**; see "Execution model" at the end.

## Context

Build a greenfield, portfolio-grade **MACH** (Microservices, API-first, Cloud-native, Headless) composable-commerce demo in `e:\Personal\MACH` (currently empty). The goal is to demonstrate cloud/platform-engineering skill: clean hexagonal architecture, real best-of-breed SaaS integrations, Terraform IaC, CI/CD, and end-to-end observability.

**Decided scope (from clarifying questions):**
- **Emphasis:** Architecture + IaC/DevOps is the headline. Clean module boundaries, Terraform, CI/CD, and observability are first-class deliverables.
- **Vendors:** Wire **real sandbox accounts** for all four — commercetools (commerce engine), Contentstack (headless CMS), Adyen (payments, test mode), Algolia (search).
- **Frontend:** A polished **Next.js (App Router, TypeScript)** headless storefront.
- **Deployment:** Terraform written as **target-topology documentation** (`terraform plan` must be green; **not applied** to a live subscription). The demo **runs locally** — no Azure cost.

**MACH mapping / stack:**
| Concern | Technology |
|---|---|
| Orchestration / BFF | **.NET 10 Azure Functions, isolated worker** (in-process is EOL), Flex Consumption plan (modeled in IaC) |
| Commerce engine | commercetools (catalog, cart, order, customer) |
| Identity / Auth | **commercetools customer authentication** via a dedicated **Auth microservice** (`Mach.Auth.Functions`) — OAuth2 password flow, anonymous sessions, `/me` endpoints; access/refresh tokens in **httpOnly+Secure+SameSite cookies**; anonymous→customer cart merge on sign-in |
| Content | Contentstack (nav, hero/marketing, PDP enrichment) |
| Payments | Adyen (sessions / Drop-in + HMAC webhooks) |
| Search | Algolia (faceted search, autocomplete) |
| Delivery & geo | Azure Maps (geocoding + distance/route matrix) — **distance-based delivery pricing & ETAs**; delivery types: **standard / express / same-day / store-pickup** (same-day gated by distance) |
| Transactional email | Azure Communication Services (ACS) Email — **multi-party notifications: customer, store, supplier, reception** (each its own template + recipient); **email copy authored in Contentstack**; local dev sink for offline runs |
| Caching | **Azure Cache for Redis** (StackExchange.Redis) behind an `ICacheStore` port — cache-aside for catalog/content/profile/delivery-quote reads; in-memory fallback for offline runs. SQL stays the source of truth for durable data |
| Relational store | SQL Server (LocalDB locally; Azure SQL in IaC) — outbox/inbox/idempotency/order read-model |
| Cloud platform | APIM, Service Bus, Key Vault, App Insights/Log Analytics, Static Web Apps, Storage |
| IaC | Terraform (azurerm ~> 4.x) |
| Frontend | Next.js App Router + TypeScript + Tailwind/shadcn |

**Prereqs to install before running:** Azure Functions Core Tools v4 (`npm i -g azure-functions-core-tools@4`), Docker Desktop (Service Bus emulator + Azurite + optional SQL), SQL Server LocalDB. (.NET 10 SDK and Terraform already present.)

---

## Architecture

**Sync flow:** `Next.js → APIM → Bff.Functions (HTTP) → vendor adapters + SQL read-models`.

**Delivery quoting (sync):** at checkout the BFF geocodes the shipping address via **Azure Maps**, computes distance to the nearest fulfilling **Store** (from SQL), and returns **delivery options per type** — standard/express priced as `base + perKm·distance` with type-specific ETAs, **same-day** offered only inside a distance threshold, **store-pickup** free. The chosen option is written back to the commercetools cart as the shipping method + external price.

**Auth microservice (`Mach.Auth.Functions`):** wraps **commercetools customer authentication** — `register` (create customer), `login` (commercetools OAuth2 **password flow** → customer access + refresh tokens), `refresh`, `anonymous` (anonymous session token so guests get a cart), `logout`, `me`. Tokens are handed to the storefront only in **httpOnly + Secure + SameSite cookies** (never localStorage). All customer-scoped work (cart, profile, order history) goes through commercetools **`/me`** endpoints using the caller's token; on sign-in the guest **anonymousId cart is merged** into the customer cart. The BFF trusts the token by round-tripping commercetools (`/me` / introspection); APIM adds rate-limiting and presence checks. *ADR alternative:* the Auth service may instead mint a short signed session JWT wrapping the customer id (with the commercetools refresh token kept server-side) so APIM can `validate-jwt` at the edge — documented, BFF-introspection chosen as the default for simplicity.

**Async flows (event-driven):**
- **Payments:** Adyen webhook → `Webhooks.Functions` verifies HMAC → dedup into `InboxEvents` → ACK `[accepted]` fast → publish to Service Bus `payments` topic → `Projection.Functions` transitions commercetools order + upserts `OrderProjection` (CQRS read model).
- **Reindex:** commercetools (via Event Grid) / Contentstack webhooks → Service Bus → `Indexer.Functions` pushes partial updates to Algolia; nightly Timer does full reconciliation.
- **Email / notifications (multi-party fan-out):** order-placed & payment-authorized/failed events → Service Bus `notifications` topic → `Notifications.Functions` resolves **four audiences** and sends a distinct templated email to each: **Customer** (order confirmation), **Store** (new order to fulfil — the assigned/nearest store), **Supplier** (replenishment/dropship request for the ordered SKUs), **Reception** (goods-in / pickup-ready notice). Each template's copy comes from **Contentstack**, personalized with order + delivery data; recipients resolved from SQL (`Stores`, `Suppliers`) with config fallbacks; sent via **ACS Email**; each send recorded in `EmailDeliveries` keyed by `OrderId+Audience+Kind` (idempotent — exactly one per audience). Local runs use a dev sink so nothing leaves the machine.
- **Outbox:** state-changing writes append to `OutboxMessages` in the same EF transaction; `Outbox.Functions` (Timer) publishes → marks sent (no dual-write inconsistency).

Webhook endpoints live in a separate Function host with per-vendor signature verification; vendors never touch SQL directly.

---

## Repository layout (monorepo)

Read it top-down as five plain-English groups: **the brain** (business rules), **the translators** (one per outside service), **the doors** (the Azure Functions that receive requests/events), then **the website, the infrastructure, and the paperwork**.

```
e:\Personal\MACH\
│
├─ 🧠 THE BRAIN — business logic, knows nothing about any vendor
│  ├─ src/Mach.Domain/              the core rules & types (Money, Order, Cart) — pure, zero dependencies
│  ├─ src/Mach.Application/          use-cases + "ports" (the wishlist of things it needs done):
│  │                                 ICommerceClient, ICustomerAuth, ICmsClient, ISearchClient,
│  │                                 IPaymentGateway, IEmailSender, IGeoLocator, ICacheStore,
│  │                                 IFulfillmentDirectory, IOutboxWriter, IOutboxReader, IIdempotencyStore
│  │                                 + DeliveryQuoting service (distance-based delivery types)
│  │                                 + NotificationFanout service (customer/store/supplier/reception)
│  └─ src/Mach.Contracts/           event messages passed between functions (versioned records)
│
├─ 🔌 THE TRANSLATORS — one folder per outside service; swap a vendor = touch ONE folder
│  ├─ src/Mach.Infrastructure.Commercetools/   talks to commercetools  (catalog, cart, orders, customer auth)
│  ├─ src/Mach.Infrastructure.Contentstack/    talks to Contentstack   (content + email copy)
│  ├─ src/Mach.Infrastructure.Algolia/         talks to Algolia        (search)
│  ├─ src/Mach.Infrastructure.Adyen/           talks to Adyen          (payments + verifies webhooks)
│  ├─ src/Mach.Infrastructure.Email/           talks to ACS Email      (+ local dev sink)
│  ├─ src/Mach.Infrastructure.Maps/            talks to Azure Maps     (geocode + distance; offline stub)
│  ├─ src/Mach.Infrastructure.Messaging/       talks to Service Bus    (+ in-memory fallback)
│  ├─ src/Mach.Infrastructure.Caching/         talks to Redis          (cache-aside; in-memory fallback)
│  └─ src/Mach.Persistence/                     talks to SQL Server     (EF Core 10, tables, migrations, fulfillment dir)
│
├─ 🚪 THE DOORS — Azure Functions apps (each is a deployable unit)
│  ├─ src/Mach.Auth.Functions/         sign-up / login / refresh / me   (HTTP — commercetools customer auth)
│  ├─ src/Mach.Bff.Functions/          the storefront's API            (HTTP)
│  ├─ src/Mach.Webhooks.Functions/     receives Adyen/CMS/commerce hooks (HTTP, verifies signatures)
│  ├─ src/Mach.Projection.Functions/   builds the "my orders" read-model (Service Bus)
│  ├─ src/Mach.Indexer.Functions/      keeps Algolia in sync            (Service Bus + nightly Timer)
│  ├─ src/Mach.Notifications.Functions/ sends order/payment emails      (Service Bus)
│  └─ src/Mach.Outbox.Functions/       reliably publishes queued events (Timer)
│
├─ 🧩 SHARED + TESTS
│  ├─ src/Mach.ServiceDefaults/     wiring every function reuses: tracing, retries, health checks
│  └─ tests/                        unit (fakes) · integration (Testcontainers SQL + WireMock) · ArchUnit dep test
│
├─ 🖥️  apps/storefront/            the website  — Next.js (App Router, TypeScript, Tailwind/shadcn)
├─ ☁️  infra/terraform/            the infrastructure-as-code — modules/ + environments/dev
├─ 🌱 seed/                        sample data loaders — commercetools / algolia / contentstack
├─ 📄 docs/                        the paperwork — architecture-plan.md, README (C4), adr/, diagrams/, vendor-setup.md, demo-script.md
├─ ⚙️  .github/workflows/          CI/CD — ci.yml (always) + deploy.yml (gated)
├─ Mach.sln                        solution file (coordinator owns edits to this)
└─ Directory.Build.props / Directory.Packages.props   shared build settings + central package versions
```

**Dependency rule (enforced by an ArchUnitNET test):** Doors → Brain + Translators; Translators → Brain (ports) only; Brain → nothing. Each vendor SDK is sealed inside its Translator project, so replacing a vendor changes exactly one folder — the visible proof of "composable."

**Key NuGet:** `Microsoft.Azure.Functions.Worker(.Sdk/.Extensions.Http.AspNetCore/.ServiceBus/.Timer)`, `MediatR`, `FluentValidation`, `Microsoft.Extensions.Http.Resilience` (Polly v8), `Azure.Monitor.OpenTelemetry.Exporter`, `Microsoft.EntityFrameworkCore.SqlServer` (10.x), `commercetools.Sdk.Api`, `Algolia.Search`, `Adyen`, `Azure.Identity`, `Azure.Messaging.ServiceBus`. Tests: `xunit`, `Testcontainers.MsSql`, `WireMock.Net`, `ArchUnitNET.xUnit`.

**Key npm (storefront):** `next`, `algoliasearch` + `react-instantsearch` + `@algolia/autocomplete-js`, `@adyen/adyen-web`, `zustand`, `@tanstack/react-query`, `@microsoft/applicationinsights-web` + react plugin, `tailwindcss` + `shadcn/ui`, `zod`.

---

## API surface (Bff.Functions, behind APIM `/api`)

`GET /catalog/categories` · `GET /search` · `GET /search/suggest` · `GET /products/{slug}` (commercetools + Contentstack merged) · `POST /carts` · `GET/PATCH /carts/{id}[/line-items]` · `POST /carts/{id}/shipping|billing` · `POST /carts/{id}/delivery-options` (Azure Maps distance → quotes per type) · `PUT /carts/{id}/delivery` (select type → sets shipping method + external price) · `GET /stores?near=` (pickup locations by distance) · `POST /checkout/{cartId}/payment-session` (commercetools cart + Adyen) · `POST /checkout/{cartId}/order` · `GET /orders/me` (SQL projection) · `GET /orders/{id}` · `GET /content/{type}/{slug}` · `GET /content/navigation` · `GET /health[/ready]`.

Auth (separate `Mach.Auth.Functions` host): `POST /auth/register` · `POST /auth/login` (commercetools password flow) · `POST /auth/refresh` · `POST /auth/anonymous` · `POST /auth/logout` · `GET /auth/me`. Sets/clears the httpOnly token cookies. Customer-scoped BFF routes (`/orders/me`, cart writes after sign-in) operate on the cookie token via commercetools `/me`.

Webhooks (separate host, IP-restricted): `POST /hooks/adyen|contentstack|commercetools`.

**Note — Algolia search runs browser-side** with a **search-only public key** (canonical Algolia pattern); the BFF owns only indexing. Documented as an ADR.

---

## SQL Server data model (EF Core 10, schema-per-concern)

SQL is the reliability backbone / read store — never source of truth for catalog/content.

| Table | Purpose |
|---|---|
| `messaging.OutboxMessages` | Transactional outbox (publish-after-commit) |
| `messaging.InboxEvents` (unique `DedupKey`) | Idempotent inbound webhook log (e.g. Adyen `pspReference:eventCode`) |
| `idempotency.IdempotencyKeys` | Honor client `Idempotency-Key` on POST cart/checkout |
| `orders.OrderProjections` / `OrderLineProjections` | CQRS read model for `/orders/me`, rebuildable from events |
| `customers.ProfileCache` | Short-TTL commercetools customer cache |
| `fulfillment.Stores` (Name, Email, Lat, Lng, ReceptionEmail) | Store/warehouse locations — power **distance-based delivery** + nearest-store + store/reception email recipients |
| `fulfillment.Suppliers` (Name, Email) + `fulfillment.ProductSuppliers` (Sku→SupplierId) | Supplier directory + SKU mapping — resolves the **supplier** notification recipient per order line |
| `notifications.EmailDeliveries` (unique `OrderId+Audience+Kind`) | One row per sent email per audience — idempotent send + delivery audit (provider message id, status) |
| `audit.WebhookDeliveries` | Forensic log (status, latency, signature result) |

Conventions: sequential-GUID PKs, UTC, `rowversion` concurrency token, JSON columns for payloads, migrations applied via `dotnet ef database update` (no auto-migrate in prod path).

---

## Storefront (Next.js App Router)

RSC-first pages: `layout` (Contentstack nav/footer + **auth/session provider** + telemetry) · `home` (Contentstack hero + commercetools featured) · `search` (Algolia InstantSearch client island) · `catalog/[category]` · `product/[slug]` PDP (commercetools commerce + Contentstack marketing block) · `login` + `register` (post to the Auth microservice, set httpOnly cookies) · `account` (profile + **order history**, protected) · `cart` · `checkout` (sign-in-or-guest → address → **delivery-type selector with live distance-based prices/ETAs + store-pickup map**, then Adyen Drop-in `ssr:false`) · `order/[id]` (shows chosen delivery + tracking-style status).

- **Auth state:** a session is established via the Auth microservice; the browser holds only httpOnly cookies. An `AuthProvider` reads `GET /auth/me` server-side to render signed-in chrome; guest checkout uses an anonymous session that merges into the customer cart on sign-in. `account`/order-history routes are guarded (redirect to `login`).

- **Cart = commercetools (server-authoritative)**; frontend holds only a cart ID (httpOnly cookie) mirrored in **Zustand** with optimistic updates carrying commercetools `version`. **TanStack Query** for client data/cache.
- Typed BFF client `lib/bff/client.ts` injects/propagates `traceparent` + `x-correlation-id`.
- **Visual composability punchline:** one product card shows commercetools price/availability + Algolia discoverability + Contentstack editorial simultaneously.

---

## Resilience & cross-cutting

- **Polly v8 (Http.Resilience)** per vendor `HttpClient`: per-attempt timeout → jittered retry on transient 5xx (never on Adyen create-payment unless idempotency-keyed) → circuit breaker → total timeout.
- **Idempotent webhooks:** verify signature → `INSERT` InboxEvents catching unique-violation = already processed.
- **Service Bus consumers:** `MaxDeliveryCount` + dead-letter; handlers check projection version (idempotent).
- **Auth/session:** commercetools customer tokens live only in **httpOnly + Secure + SameSite=Lax cookies**; the storefront never sees raw tokens. Access-token expiry triggers a silent `/auth/refresh`; `logout` revokes + clears cookies. CSRF mitigated by SameSite + a double-submit token on state-changing calls. commercetools API-client secret stays server-side (Key Vault / user-secrets).
- **Secrets:** prod = Key Vault references resolved via system-assigned **managed identity** (no secrets in app settings or Terraform state); local = .NET user-secrets + gitignored `local.settings.json`.
- **Observability:** `Mach.ServiceDefaults` wires OpenTelemetry (traces/metrics/logs) → App Insights; W3C `traceparent` propagates storefront → APIM → BFF → vendor dependency spans → Service Bus. Per-vendor health checks; custom metrics (reindex lag, webhook latency, cart conversion).

---

## Terraform (IaC as target-topology documentation)

```
infra/terraform/
  modules/  naming  monitoring(LogAnalytics+AppInsights)  keyvault  storage
            sql(Azure SQL + AAD admin)  servicebus(topics: payments/catalog/content/notifications + DLQ)
            communication(Azure Communication Services + Email Communication Service + managed domain)
            maps(Azure Maps account — geocoding + route/distance, key in Key Vault)
            redis(Azure Cache for Redis — cache-aside store, connection string in Key Vault)
            functions(Flex Consumption via functionAppConfig, Linux, dotnet-isolated)
            apim(API defs + policies + named values)  network(optional VNet/private endpoints)
  environments/dev/  (main.tf composition root, variables.tf, terraform.tfvars[non-secret], backend.tf)
  versions.tf
```

- **Flex Consumption** modeled with `functionAppConfig` (runtime dotnet-isolated, instanceMemoryMB, maxInstanceCount, MI-auth deployment storage). One app per Flex plan → instantiate the `functions` module per host, or consolidate to 2 apps (api + workers) — documented trade-off.
- **State:** azurerm remote backend block + a commented local backend so reviewers can `init`/`plan` offline. **`terraform plan` is the deliverable, not `apply`.**
- **Secrets:** vendor keys as `sensitive` variables sourced from `TF_VAR_*`; Terraform creates **empty Key Vault secret placeholders** referenced by app settings — no plaintext in state.
- **RBAC over keys:** managed identities granted Key Vault Secrets User, Service Bus Data Sender/Receiver, Storage Blob Data Owner, SQL AAD — passwordless posture. Each module ships a README (inputs/outputs/purpose).

---

## CI/CD (GitHub Actions)

**`ci.yml`** (PR + main, path-filtered, parallel): `lint-frontend` (eslint/tsc/prettier) · `test-frontend` (vitest + Playwright smoke) · `build-frontend` (`next build`) · `build-test-dotnet` (`dotnet build -warnaserror` + xUnit + coverage) · `terraform-validate` (`fmt -check`, `init -backend=false`, `validate`, `plan` → PR comment) · `security` (gitleaks, trivy fs, `dotnet list package --vulnerable`, `pnpm audit`, CodeQL JS/C#) · `status-gate`.

**`deploy.yml`** (gated, `workflow_dispatch`, Environment `azure-demo` w/ required reviewer, **OIDC federated login — no stored cloud secrets**): `deploy-functions` (`dotnet publish` + functions-action) and `deploy-swa` (static-web-apps-deploy). Exists as production-grade documentation; never fires in the demo.

---

## Seed data

Idempotent scripts, run order **commercetools → Algolia → Contentstack**:
- **commercetools:** `@commercetools/platform-sdk` — one apparel product type, ~20–30 products / 3–4 categories, prices, inventory, a few discounts (strike-through). Shipping methods for the **delivery types** (standard/express/same-day/store-pickup). Upsert by SKU.
- **SQL fulfillment seed:** a handful of **Stores** (name, email, reception email, lat/lng across a region so distance varies) and **Suppliers** + SKU→supplier mappings — drives distance quoting and the store/supplier/reception recipients.
- **Algolia:** flatten commercetools products → records; configure searchableAttributes, attributesForFaceting, customRanking, query-suggestions.
- **Contentstack:** CMA scripts create content types (Home Hero, Promo Tile, Navigation, PDP Marketing Block, Footer, **Email Template** — subject + body with `{{order}}`/`{{delivery}}` tokens, one entry **per audience**: customer/store/supplier/reception) + product-keyed entries; publish to delivery environment.

---

## Local-run story (no Azure)

1. **SQL:** LocalDB `(localdb)\MSSQLLocalDB` (or mssql container) → `dotnet ef database update`.
2. **Storage:** Azurite (`AzureWebJobsStorage=UseDevelopmentStorage=true`).
3. **Service Bus:** Docker emulator with `config.json` (topics payments/catalog/content/notifications), `UseDevelopmentEmulator=true`. **Fallback:** `IMessageBus` in-memory implementation (`Messaging:Provider=InMemory`) so it runs without Docker.
4. **Vendors:** real sandbox keys in user-secrets / `local.settings.json` (gitignored). Adyen webhooks via dev tunnel/ngrok, or a bundled "replay sample notification" script for fully offline demo.
5. **Email:** local dev uses a sink (smtp4dev container or an `.eml`-to-`./mail/` writer) selected by `Email:Provider=DevSink`; ACS used only with real keys. The four audience recipients (customer/store/supplier/reception) come from the SQL seed + `Notifications:*` config so all four `.eml` files appear in `./mail/`.
6. **Maps:** real Azure Maps key in user-secrets, or `Maps:Provider=Stub` (haversine over seeded store lat/lng) so distance-based delivery works fully offline.
7. **Cache:** real Redis container (`docker run redis`) with `Cache:Provider=Redis`, or `Cache:Provider=InMemory` so caching works with no Redis. SQL remains the source of truth — caching is purely an accelerator.
8. **Run:** `func start` per host (distinct ports) + `next dev` against `http://localhost:7071/api` (APIM bypassed locally, BFF CORS for dev). A `run.ps1` orchestrates: Azurite → SB emulator → ef update → hosts → Next.js.

---

## Documentation deliverables (portfolio signal)

- **README:** pitch → **C4 Mermaid** (system-context + container) → quickstart → screenshot/GIF → MACH-mapping table.
- **ADRs (MADR):** MACH rationale · BFF pattern · Algolia browser key · cart = commercetools + Zustand · OIDC/no-secrets CI · Terraform-as-docs · OTel choice · **distance-based delivery & external shipping price on commercetools** · **multi-party notification fan-out (customer/store/supplier/reception)** · **commercetools customer auth as the identity provider (no separate IdP) + httpOnly-cookie session, BFF-introspection vs edge-JWT trade-off** · **Redis cache-aside (SQL stays source of truth) + invalidation on change events**.
- **vendor-setup.md:** step-by-step sandbox creation for all four, mapping each to `.env.example` (which keys are public vs secret).
- **Checkout sequence diagram** (Mermaid) with correlation-id annotations; context/data-ownership map.
- **demo-script.md:** 3–5 min walkthrough mapped to MACH — Headless (Contentstack home) → API-first + Microservices (Algolia search → PDP with commerce+content) → **sign in via the commercetools-backed Auth microservice (guest cart merges into the customer cart)** → cart → **enter address: delivery types re-price live by distance (Azure Maps), pick same-day vs pickup** → Adyen checkout w/ 3DS test card → **four emails (customer/store/supplier/reception) land in the dev sink, copy authored in Contentstack** (event-driven async proof) → Cloud-native (App Insights Application Map fanning to all vendors + GitHub Actions run + Terraform plan).

---

## Execution model (subagent-driven, parallel waves)

The main session acts as **coordinator**: it holds the contracts, dispatches one **implementer subagent per task** with the full task text + curated context (subagents never read this plan or inherit session history), and after each task runs **two reviews in order — spec compliance, then code quality** — looping fixes back to the same implementer until both pass before marking the task done (`TodoWrite`). A **final whole-implementation review** runs after the last wave.

**Parallelism rule:** tasks within a wave run **concurrently only when their file sets are disjoint**. The MACH layout makes this natural — every adapter, host, the Terraform tree, docs, seed, and the storefront live in separate folders. The **only shared files** are `Mach.sln` and `Directory.Packages.props`. Mitigation (pick one per wave): **(a)** run each parallel track in its own **git worktree** (`superpowers:using-git-worktrees`) and integrate on return, or **(b)** keep tracks in one tree but have the coordinator **serialize** the `dotnet sln add` / package-version edits as a short post-step after each track reports back. Implementers within a wave are told their exact subtree and forbidden from editing shared root files (coordinator owns those).

**Model tiering:** mechanical single-project tasks (most adapters, seed scripts) → cheaper/faster model; multi-project composition (hosts, storefront wiring) → standard model; architecture/review/Terraform → most capable model.

### Wave 0 — Foundation *(sequential, single agent — establishes shared contracts everyone codes against)*
Repo skeleton + `Mach.sln` + `Directory.Build.props`/`Directory.Packages.props`; `Mach.Domain`; `Mach.Application` **ports + MediatR scaffolding**; `Mach.Contracts` integration events; `Mach.ServiceDefaults` (OTel + Polly registry + health); ArchUnitNET dependency test; CI skeleton (`dotnet build/test`, `terraform fmt -check`/`validate`). **Also: copy this plan → `docs/architecture-plan.md`** and stub `docs/adr/`, `docs/README.md`.

### Wave 1 — Independent building blocks *(PARALLEL — fully disjoint subtrees)*
| Track | Subtree | Notes |
|---|---|---|
| 1a Persistence | `src/Mach.Persistence` + tests | EF Core DbContext, all tables/migrations, outbox/inbox/idempotency repos, Testcontainers MsSql tests |
| 1b commercetools adapter | `src/Mach.Infrastructure.Commercetools` | implements `ICommerceClient` **+ `ICustomerAuth`** (password flow, anonymous session, `/me`, cart merge), Polly, WireMock tests |
| 1c Algolia adapter | `src/Mach.Infrastructure.Algolia` | implements `ISearchClient` |
| 1d Contentstack adapter | `src/Mach.Infrastructure.Contentstack` | implements `ICmsClient` |
| 1e Adyen adapter | `src/Mach.Infrastructure.Adyen` | implements `IPaymentGateway` + HMAC verifier |
| 1e′ Email adapter | `src/Mach.Infrastructure.Email` | implements `IEmailSender` — ACS Email + **dev sink** fallback, template rendering |
| 1e″ Maps adapter | `src/Mach.Infrastructure.Maps` | implements `IGeoLocator` — Azure Maps geocode+distance + **haversine stub** |
| 1f Messaging infra | `src/Mach.Infrastructure.Messaging` | Service Bus sender + **in-memory fallback** + outbox dispatcher logic |
| 1g Terraform | `infra/terraform` | all modules + `environments/dev`, Flex `functionAppConfig`, Key Vault placeholders + RBAC, green `terraform plan`, module READMEs |
| 1h Docs | `docs/` | C4 Mermaid, ADRs, `vendor-setup.md`, context map |
| 1i Seed | `seed/` | commercetools → Algolia → Contentstack idempotent scripts |
| 1j Storefront shell | `apps/storefront` | Next.js scaffold, Tailwind/shadcn, typed BFF client against `Mach.Contracts`, layout/theme/cart provider |

*Each track codes against Wave 0 ports/contracts with mocked vendor responses — no cross-track dependency. Coordinator serializes `.sln`/package additions as tracks return.*

### Wave 2 — Composition hosts *(PARALLEL among 2a–2f; 2g after 2a+2b)*
| Track | Subtree | Depends on |
|---|---|---|
| 2·Auth host | `src/Mach.Auth.Functions` | 1b — register/login/refresh/anonymous/logout/me + cookie handling (run early; BFF customer-scoped routes depend on the session model) |
| 2a BFF read endpoints | `src/Mach.Bff.Functions` | 1b,1c,1d |
| 2b BFF cart + checkout + **delivery** | `src/Mach.Bff.Functions` (cart/checkout area) | 1b,1e,1e″,1a — delivery-options/quoting + Adyen; sequence after 2a (same project) |
| 2c Webhooks host | `src/Mach.Webhooks.Functions` | 1e,1f,1a |
| 2d Projection host | `src/Mach.Projection.Functions` | 1b,1a,1f |
| 2e Indexer host | `src/Mach.Indexer.Functions` | 1c,1d,1f |
| 2f Outbox host | `src/Mach.Outbox.Functions` | 1a,1f |
| 2f′ Notifications host | `src/Mach.Notifications.Functions` | 1e′,1d,1a,1f — fan-out to **customer/store/supplier/reception**, each a Contentstack-templated email |
| 2g Storefront features | `apps/storefront` (pages) | 2a,2b — wire real BFF: home/search/PDP/cart/Adyen checkout/order |

*2a and 2b share `Mach.Bff.Functions`, so run them sequentially; the other hosts are disjoint and parallel-safe.*

### Wave 3 — Integration & polish *(coordinated, mostly sequential)*
APIM API definitions + policies; App Insights workbook + KQL; DLQ handling + resilience/chaos test; `run.ps1` + `docs/run-local.md`; checkout sequence diagram; `docs/demo-script.md`; finalize ADRs. Then **final whole-implementation code review** → `superpowers:finishing-a-development-branch`.

**Checkpointable:** any wave is a coherent stopping point — Wave 0+1a/1b/1c/1d+2a+1j+2g already yields a browsable composable storefront (search + PDP) before payments/async exist.

---

## Verification

- **Per layer:** `dotnet build -warnaserror` + `dotnet test` (unit + Testcontainers integration + ArchUnit dependency test) green.
- **IaC:** `terraform fmt -check`, `terraform validate`, and **`terraform plan`** succeed offline (`-backend=false`).
- **Storefront:** `next build` + `tsc --noEmit` + eslint clean; Playwright smoke renders home/search/PDP/checkout.
- **End-to-end (local):** run `run.ps1`, then walk the demo script — browse (Contentstack+commercetools) → Algolia search/autocomplete → PDP enrichment → **register + login through the Auth microservice (httpOnly cookies set; anonymous cart merges into the customer cart)** → add to cart → **enter two different addresses and confirm delivery prices/ETAs change with distance and same-day disables beyond the threshold** → pick a delivery type → Adyen test-card checkout w/ 3DS → order confirmation reflects the chosen delivery → `GET /orders/me` reflects the projection → **four `.eml` files (customer/store/supplier/reception) appear in `./mail/` with Contentstack-authored copy** → App Insights/console trace shows spans fanning to all vendors.
- **CI:** open a PR; confirm all `ci.yml` jobs (lint, tests, terraform plan comment, security scans) pass and `deploy.yml` remains gated.
