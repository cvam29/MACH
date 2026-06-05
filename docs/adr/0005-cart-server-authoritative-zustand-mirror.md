# 0005 — Cart is commercetools server-authoritative, mirrored in Zustand

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** frontend, state, consistency

## Context and Problem Statement

The cart drives revenue and must stay consistent with pricing, inventory, and
discounts. Where does cart state **live**, and how does the storefront present a
responsive UI without becoming a second source of truth?

## Decision Drivers

- Correctness: prices, promotions, and availability must come from commerce.
- Responsive UX (optimistic add/update) without race conditions.
- Avoid a client-side cart that can drift from the server.
- Concurrency safety against commercetools' optimistic-locking model.

## Considered Options

1. **Client-owned cart** (full cart in Zustand/localStorage, synced periodically).
2. **Server-authoritative commercetools cart, thin client mirror** — browser
   holds only a cart id (httpOnly cookie) + a mirror in Zustand carrying the
   commercetools `version`.
3. **Server-only, no client cache** — re-fetch the cart on every interaction.

## Decision Outcome

**Chosen option: "Server-authoritative commercetools cart, thin client mirror."**
commercetools is the cart source of truth. The browser stores only the **cart
id** (httpOnly cookie); **Zustand** mirrors cart contents for optimistic updates,
each carrying the commercetools **`version`** for concurrency. **TanStack Query**
handles client data/cache and reconciliation. The cart merges anonymous→customer
on sign-in (see [ADR 0003](0003-commercetools-customer-auth-identity-provider.md)).

## Consequences

- **Good:** Pricing/inventory/discounts are always authoritative; no drift.
- **Good:** Optimistic UI feels instant; `version` conflicts are detected and
  reconciled by re-fetching rather than silently overwriting.
- **Good:** No sensitive cart identifiers exposed to JS (httpOnly cookie).
- **Bad / trade-off:** Every mutation round-trips commercetools and must pass the
  current `version`; mitigated by optimistic updates + query invalidation.
- **Neutral:** Establishes the `version`-aware client contract reused by checkout.

## More Information

- [Architecture plan — Storefront](../architecture-plan.md)
- [Context map — Cart ownership](../diagrams/context-map.md)
