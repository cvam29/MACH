# Architecture Decision Records

[MADR](https://adr.github.io/madr/)-format records of the significant decisions
behind this MACH demo. New ADRs copy [`0000-template.md`](0000-template.md) and
take the next number; ADRs are immutable once Accepted (supersede rather than
edit).

| # | Decision | Status |
|---|---|---|
| [0001](0001-mach-composable-architecture.md) | Adopt a MACH / composable-commerce architecture | Accepted |
| [0002](0002-dotnet-functions-bff-pattern.md) | .NET Azure Functions Backend-for-Frontend (BFF) | Accepted |
| [0003](0003-commercetools-customer-auth-identity-provider.md) | commercetools customer auth as the identity provider (httpOnly-cookie session; BFF-introspection vs edge-JWT) | Accepted |
| [0004](0004-algolia-browser-search-key.md) | Algolia search browser-side with a search-only key | Accepted |
| [0005](0005-cart-server-authoritative-zustand-mirror.md) | Cart = commercetools server-authoritative + Zustand mirror | Accepted |
| [0006](0006-distance-based-delivery-external-shipping-price.md) | Distance-based delivery & external shipping price on the cart | Accepted |
| [0007](0007-multi-party-notification-fan-out.md) | Multi-party notification fan-out (customer/store/supplier/reception) | Accepted |
| [0008](0008-oidc-no-long-lived-cloud-secrets-ci.md) | OIDC federation in CI/CD: no long-lived cloud secrets | Accepted |
| [0009](0009-terraform-as-documentation.md) | Terraform as target-topology documentation (plan, not apply) | Accepted |
| [0010](0010-opentelemetry-end-to-end-tracing.md) | OpenTelemetry for end-to-end tracing | Accepted |
| [0011](0011-redis-cache-aside.md) | Redis cache-aside with event-driven invalidation (SQL stays source of truth) | Accepted |
