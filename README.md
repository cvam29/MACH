# MACH — Composable Commerce Demo

A greenfield, portfolio-grade **MACH** (Microservices, API-first, Cloud-native, Headless) composable-commerce platform: a polished Next.js storefront over a fleet of **.NET 10 Azure Functions** (isolated worker) — a dedicated **Auth microservice** and a **BFF** — orchestrating best-of-breed SaaS (commercetools, Contentstack, Adyen, Algolia, Azure Maps, ACS Email) on an Azure backbone (Service Bus, SQL, Key Vault, App Insights), provisioned with **Terraform** and shipped through **OIDC-secured CI/CD** with **OpenTelemetry** end-to-end tracing. It runs entirely locally — no Azure spend — while the infrastructure-as-code stands as production-ready target-topology documentation.

> This repository is an **architecture and platform-engineering showcase**. The emphasis is on clean hexagonal module boundaries, IaC/DevOps, and observability rather than feature breadth.

---

## MACH in this repository

| MACH pillar | How this repo embodies it |
|---|---|
| **M**icroservices | Independently deployable Azure Functions hosts — `Mach.Auth.Functions`, `Mach.Bff.Functions`, `Mach.Webhooks.Functions`, `Mach.Projection.Functions`, `Mach.Indexer.Functions`, `Mach.Notifications.Functions`, `Mach.Outbox.Functions` — each a separate unit of deployment and scale, communicating over HTTP and Azure Service Bus. |
| **A**PI-first | Every capability is exposed as a versioned HTTP contract behind APIM (`/api/...`); the storefront has **no** direct vendor coupling for commerce/identity — it talks only to the BFF and Auth APIs. Event contracts in `Mach.Contracts` are versioned records. |
| **C**loud-native | Stateless Functions on **Flex Consumption**, Service Bus topics for async/event-driven flows, transactional **outbox/inbox** for reliability, passwordless **managed-identity + Key Vault** secrets, OpenTelemetry → App Insights, all provisioned by **Terraform** (`azurerm ~> 4.x`). |
| **H**eadless | The Next.js storefront is fully decoupled: **commercetools** owns commerce + identity, **Contentstack** owns content + email copy, **Algolia** owns search — composed at the edge. Swapping a vendor touches exactly **one** translator project. |

---

## System Context (C4)

```mermaid
C4Context
    title System Context — MACH Composable Commerce

    Person(shopper, "Shopper", "Browses, searches, buys")

    System_Boundary(mach, "MACH Demo Platform") {
        System(storefront, "Storefront", "Next.js App Router SPA/RSC")
        System(auth, "Auth Microservice", ".NET Functions — commercetools customer auth")
        System(bff, "BFF", ".NET Functions — API-first orchestration")
    }

    System_Ext(ct, "commercetools", "Commerce engine + customer identity")
    System_Ext(cs, "Contentstack", "Content + email copy")
    System_Ext(adyen, "Adyen", "Payments (test)")
    System_Ext(algolia, "Algolia", "Search index")
    System_Ext(maps, "Azure Maps", "Geocode + distance")
    System_Ext(acs, "ACS Email", "Transactional email")
    System_Ext(azure, "Azure Platform", "Service Bus, SQL, Key Vault, App Insights")

    Rel(shopper, storefront, "Uses", "HTTPS")
    Rel(storefront, auth, "Login / session", "HTTPS + httpOnly cookie")
    Rel(storefront, bff, "Reads/writes commerce", "HTTPS")
    Rel(storefront, algolia, "Searches", "browser search-only key")

    Rel(auth, ct, "OAuth2 password / anonymous / me")
    Rel(bff, ct, "Catalog, cart, order")
    Rel(bff, cs, "Content + email copy")
    Rel(bff, adyen, "Payment session")
    Rel(bff, maps, "Delivery distance")
    Rel(bff, algolia, "Indexing")
    Rel(bff, azure, "Events, read-models, secrets")
    Rel(azure, acs, "Sends notifications")
```

---

## Container Diagram (C4)

```mermaid
C4Container
    title Container Diagram — MACH Demo

    Person(shopper, "Shopper", "Browser")

    System_Boundary(mach, "MACH Platform") {
        Container(sf, "Storefront", "Next.js App Router, TypeScript", "RSC pages + client islands")
        Container(apim, "APIM", "Azure API Management", "Rate-limit, routing, JWT presence")
        Container(auth, "Auth Functions", ".NET 10 isolated Functions", "register/login/refresh/anonymous/me; httpOnly cookies")
        Container(bff, "BFF Functions", ".NET 10 isolated Functions", "Catalog, cart, delivery quoting, checkout")
        Container(hooks, "Webhooks Functions", ".NET 10 isolated Functions", "Adyen/CMS/commerce signature verify")
        Container(proj, "Projection Functions", ".NET 10 isolated Functions", "Order read-model CQRS")
        Container(idx, "Indexer Functions", ".NET 10 isolated Functions", "Algolia sync + nightly Timer")
        Container(notif, "Notifications Functions", ".NET 10 isolated Functions", "4-audience email fan-out")
        Container(outbox, "Outbox Functions", ".NET 10 isolated Functions", "Publish-after-commit Timer")
        ContainerDb(sql, "SQL Server", "Azure SQL / LocalDB", "Outbox, inbox, idempotency, read-models, fulfillment")
        ContainerQueue(sb, "Service Bus", "Topics: payments/catalog/content/notifications", "Async backbone")
    }

    System_Ext(ct, "commercetools", "Commerce + identity")
    System_Ext(cs, "Contentstack", "Content + email copy")
    System_Ext(adyen, "Adyen", "Payments")
    System_Ext(algolia, "Algolia", "Search")
    System_Ext(maps, "Azure Maps", "Geo")
    System_Ext(acs, "ACS Email", "Email")

    Rel(shopper, sf, "Uses", "HTTPS")
    Rel(sf, algolia, "Search", "search-only key")
    Rel(sf, apim, "API calls", "HTTPS")
    Rel(apim, auth, "Routes auth")
    Rel(apim, bff, "Routes commerce")
    Rel(auth, ct, "OAuth2 / me")
    Rel(bff, ct, "Cart/order")
    Rel(bff, cs, "Content")
    Rel(bff, adyen, "Payment session")
    Rel(bff, maps, "Distance")
    Rel(bff, sql, "Read-models")
    Rel(bff, sb, "Publish via outbox")
    Rel(hooks, ct, "Verify + dedup")
    Rel(hooks, sql, "Inbox")
    Rel(hooks, sb, "Publish")
    Rel(outbox, sql, "Drain outbox")
    Rel(outbox, sb, "Publish")
    Rel(proj, sb, "Consume payments")
    Rel(proj, ct, "Transition order")
    Rel(proj, sql, "Upsert projection")
    Rel(idx, sb, "Consume catalog/content")
    Rel(idx, algolia, "Index")
    Rel(notif, sb, "Consume notifications")
    Rel(notif, cs, "Email copy")
    Rel(notif, sql, "Resolve recipients")
    Rel(notif, acs, "Send 4 emails")
```

---

## Tech stack

| Concern | Technology |
|---|---|
| Orchestration / BFF | .NET 10 Azure Functions (isolated worker), Flex Consumption |
| Identity / Auth | commercetools customer authentication via `Mach.Auth.Functions` (OAuth2 password + anonymous), httpOnly-cookie sessions |
| Commerce engine | commercetools (catalog, cart, order, customer) |
| Content | Contentstack (navigation, marketing, PDP enrichment, email copy) |
| Payments | Adyen (sessions / Drop-in + HMAC webhooks, test mode) |
| Search | Algolia (faceted search, autocomplete, browser search-only key) |
| Delivery & geo | Azure Maps (geocode + distance/route matrix) — distance-based delivery pricing/ETAs |
| Transactional email | Azure Communication Services (ACS) Email + local dev sink |
| Relational store | SQL Server (LocalDB locally, Azure SQL in IaC) — outbox/inbox/idempotency/read-models |
| Messaging | Azure Service Bus (topics: payments/catalog/content/notifications) + in-memory fallback |
| Cloud platform | APIM, Key Vault, App Insights / Log Analytics, Static Web Apps, Storage |
| IaC | Terraform (`azurerm ~> 4.x`) |
| CI/CD | GitHub Actions (path-filtered CI; OIDC-federated gated deploy) |
| Observability | OpenTelemetry (traces/metrics/logs) → Azure Monitor / App Insights |
| Frontend | Next.js App Router + TypeScript + Tailwind / shadcn, Zustand, TanStack Query |

---

## Run locally

The platform runs entirely on your machine with no Azure cost (Azurite + Service Bus emulator/in-memory + LocalDB + vendor sandboxes or offline stubs).

> **Quickstart:** see [`docs/run-local.md`](docs/run-local.md) for the full `run.ps1` orchestration (Azurite → Service Bus emulator → `dotnet ef database update` → Functions hosts → `next dev`).
>
> _`docs/run-local.md` is added in a later milestone (Wave 3); until then, follow the "Local-run story" section of [`docs/architecture-plan.md`](docs/architecture-plan.md)._

Before configuring vendors, copy [`.env.example`](.env.example) and follow [`docs/vendor-setup.md`](docs/vendor-setup.md) to create the sandbox accounts and map each credential.

---

## Repository layout

Read the monorepo top-down as five plain-English groups — **the brain**, **the translators**, **the doors**, then the website, infrastructure, and paperwork:

- 🧠 **The brain** — business logic that knows nothing about any vendor: `src/Mach.Domain` (pure core types), `src/Mach.Application` (use-cases + ports: `ICommerceClient`, `ICustomerAuth`, `ICmsClient`, `ISearchClient`, `IPaymentGateway`, `IEmailSender`, `IGeoLocator`, …, plus `DeliveryQuoting` and `NotificationFanout` services), `src/Mach.Contracts` (versioned event records).
- 🔌 **The translators** — one folder per outside service; swapping a vendor touches exactly one: `Mach.Infrastructure.Commercetools` / `.Contentstack` / `.Algolia` / `.Adyen` / `.Email` / `.Maps` / `.Messaging` and `Mach.Persistence` (SQL/EF Core).
- 🚪 **The doors** — Azure Functions apps, each a deployable unit: `Mach.Auth.Functions`, `Mach.Bff.Functions`, `Mach.Webhooks.Functions`, `Mach.Projection.Functions`, `Mach.Indexer.Functions`, `Mach.Notifications.Functions`, `Mach.Outbox.Functions`.
- 🖥️ **The website** — `apps/storefront` (Next.js App Router).
- ☁️ **The infrastructure** — `infra/terraform` (modules + `environments/dev`).
- 🌱 **Seed** — `seed/` (commercetools → Algolia → Contentstack idempotent loaders).
- 📄 **The paperwork** — `docs/` (this README, ADRs, diagrams, vendor setup, architecture plan).

**Dependency rule** (enforced by an ArchUnitNET test): Doors → Brain + Translators; Translators → Brain (ports) only; Brain → nothing. Each vendor SDK is sealed inside its translator — the visible proof of "composable."

---

## Documentation

- 📐 [`docs/architecture-plan.md`](docs/architecture-plan.md) — the authoritative, living architecture & execution plan (start here for the full picture).
- 🧭 [Architecture Decision Records](docs/adr/) — MADR-format ADRs for every significant choice.
- 🔌 [`docs/vendor-setup.md`](docs/vendor-setup.md) — step-by-step sandbox creation and credential mapping.
- 🔁 [`docs/diagrams/checkout-sequence.md`](docs/diagrams/checkout-sequence.md) — end-to-end checkout sequence with correlation-id annotations.
- 🗺️ [`docs/diagrams/context-map.md`](docs/diagrams/context-map.md) — data-ownership / context map.
