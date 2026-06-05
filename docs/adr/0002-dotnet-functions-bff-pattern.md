# 0002 — .NET Azure Functions Backend-for-Frontend (BFF)

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** architecture, backend, security

## Context and Problem Statement

The Next.js storefront needs commerce data composed from several vendors
(commercetools + Contentstack + delivery/payment orchestration). Should the
browser talk to vendors directly, or through a backend tier? And what runtime
should that tier use?

## Decision Drivers

- **API-first** — a single, versioned, vendor-agnostic surface for the storefront.
- Keep vendor secrets and orchestration **server-side**.
- Cloud-native, scale-to-zero, pay-per-use; minimal ops.
- Showcase .NET 10 isolated-worker Functions (current best practice).

## Considered Options

1. **Direct-to-vendor from the browser** — storefront calls each SaaS SDK.
2. **Long-running container/API (ASP.NET on App Service/AKS)** as the BFF.
3. **.NET 10 Azure Functions (isolated worker) BFF** on Flex Consumption.

## Decision Outcome

**Chosen option: ".NET 10 Azure Functions (isolated worker) BFF."**
`Mach.Bff.Functions` exposes the storefront API (`/catalog`, `/search`,
`/products`, `/carts`, `/checkout`, `/orders`, `/content`, …) behind APIM. The
in-process model is EOL, so we use the **isolated worker**; Flex Consumption is
modeled in IaC. Cross-cutting concerns live in `Mach.ServiceDefaults`.

## Consequences

- **Good:** Browser couples only to our contracts; vendors, secrets, and
  composition stay server-side (one product card merges commercetools price +
  Algolia discoverability + Contentstack editorial).
- **Good:** Per-function deploy/scale; scale-to-zero suits a bursty demo and
  costs nothing locally.
- **Good:** Clean DI/middleware story for OTel, Polly resilience, and auth.
- **Bad / trade-off:** Cold starts and a slightly more involved local dev loop
  (Core Tools + Azurite); acceptable for a demo and mitigated by `run.ps1`.
- **Neutral:** APIM fronts the BFF for rate-limiting and routing; locally APIM is
  bypassed and the storefront hits the Functions host directly.

## More Information

- [Architecture plan — Architecture / API surface](../architecture-plan.md)
- Related: [ADR 0003](0003-commercetools-customer-auth-identity-provider.md) (Auth host),
  [ADR 0010](0010-opentelemetry-end-to-end-tracing.md) (tracing).
