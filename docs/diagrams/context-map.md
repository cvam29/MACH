# Context / Data-Ownership Map

Which system **owns** which data. The golden rule: SQL Server is the
*reliability backbone and read store* — it is **never** the source of truth for
catalog, content, identity, or search. Each external system owns its domain;
everything in SQL is either operational plumbing (outbox/inbox/idempotency) or a
**derived/rebuildable** projection.

```mermaid
graph TB
    subgraph CT["commercetools — system of record"]
        CT1[Catalog and prices]
        CT2[Cart - server authoritative]
        CT3[Orders]
        CT4[Customer identity and auth]
    end

    subgraph CS["Contentstack — content authority"]
        CS1[Navigation and marketing]
        CS2[PDP enrichment]
        CS3[Email copy per audience]
    end

    subgraph AL["Algolia — search index - derived"]
        AL1[Product search records]
        AL2[Query suggestions]
    end

    subgraph SQL["SQL Server — reliability and read store"]
        SQL1[Outbox / Inbox]
        SQL2[Idempotency keys]
        SQL3[Order projections - CQRS]
        SQL4[Stores / Suppliers / mappings]
        SQL5[EmailDeliveries audit]
        SQL6[ProfileCache - short TTL]
    end

    subgraph EXT["Stateless services"]
        MAP[Azure Maps - geo compute]
        AD[Adyen - payment state]
        ACS[ACS Email - delivery]
    end

    CT1 -.flatten/index.-> AL1
    CT1 -.cache.-> SQL6
    CT3 -.project.-> SQL3
    CS3 -.render with order data.-> ACS
    SQL4 -.recipients.-> ACS
    MAP -.distance.-> CT2
    AD -.webhook.-> SQL1

    classDef sor fill:#1f6feb,stroke:#0b3d91,color:#fff
    classDef derived fill:#8957e5,stroke:#5a2da0,color:#fff
    classDef store fill:#1a7f37,stroke:#0b5023,color:#fff
    class CT1,CT2,CT3,CT4 sor
    class AL1,AL2 derived
    class SQL1,SQL2,SQL3,SQL4,SQL5,SQL6 store
```

## Ownership table

| Data | Owner (source of truth) | Notes / derivation |
|---|---|---|
| Catalog, prices, inventory | **commercetools** | Flattened into Algolia and short-TTL-cached in `customers.ProfileCache` (customer) / read paths; never authored in SQL. |
| Cart | **commercetools** (server-authoritative) | Browser holds only a cart id (httpOnly cookie) mirrored in Zustand with the cart `version`. See [ADR 0005](../adr/0005-cart-server-authoritative-zustand-mirror.md). |
| Orders | **commercetools** | Projected into `orders.OrderProjections` for `/orders/me` (CQRS, rebuildable from events). |
| Customer identity & auth | **commercetools** | The identity provider; no separate IdP. See [ADR 0003](../adr/0003-commercetools-customer-auth-identity-provider.md). |
| Navigation, marketing, PDP enrichment | **Contentstack** | Composed with commercetools commerce data at the edge. |
| Email copy (per audience) | **Contentstack** | Subject/body templates with `{{order}}`/`{{delivery}}` tokens; rendered by `Notifications.Functions`. See [ADR 0007](../adr/0007-multi-party-notification-fan-out.md). |
| Search index & suggestions | **Algolia** | Derived from commercetools by `Indexer.Functions`; fully rebuildable. Browser uses a search-only key. See [ADR 0004](../adr/0004-algolia-browser-search-key.md). |
| Geo distance / ETA | **Azure Maps** (stateless) | Computed on demand; result drives the cart's external shipping price. See [ADR 0006](../adr/0006-distance-based-delivery-external-shipping-price.md). |
| Payment state | **Adyen** (stateless to us) | Authoritative via HMAC webhook → `messaging.InboxEvents` (dedup) → projection. |
| Email delivery | **ACS Email** (stateless to us) | Each send audited in `notifications.EmailDeliveries` (`OrderId+Audience+Kind`). |
| Outbox / Inbox / Idempotency | **SQL Server** | Operational reliability plumbing only — the one place SQL *is* authoritative. |
| Stores / Suppliers / SKU→Supplier | **SQL Server** | Operational reference data driving distance quoting + notification recipients. |
