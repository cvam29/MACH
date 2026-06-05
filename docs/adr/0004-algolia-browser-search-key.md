# 0004 — Algolia search runs browser-side with a search-only key

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** search, frontend, performance

## Context and Problem Statement

The storefront has faceted search and autocomplete. Algolia is designed to be
queried directly from the browser. Should search queries go **through the BFF**
(proxy) or **directly from the browser** to Algolia?

## Decision Drivers

- Lowest possible search latency and best UX (InstantSearch / Autocomplete are
  built for direct, client-side querying).
- Avoid turning the BFF into a hot proxy for every keystroke.
- Keep write/admin capability locked down server-side.
- Follow the **canonical Algolia pattern**.

## Considered Options

1. **BFF proxy** — browser → BFF → Algolia for every search/suggest call.
2. **Browser-direct with a search-only key** — browser → Algolia; BFF owns only
   indexing with the admin key.

## Decision Outcome

**Chosen option: "Browser-direct with a search-only key."** The storefront ships
the **search-only public API key** and queries Algolia directly via
`react-instantsearch` / `@algolia/autocomplete-js`. `Indexer.Functions` and the
seed scripts are the **only** holders of the **admin key**.

## Consequences

- **Good:** Minimal latency, no per-keystroke BFF load, idiomatic Algolia.
- **Good:** The public key is read-only and index-scoped — limited blast radius;
  the admin key never leaves the server.
- **Bad / trade-off:** Search query shape is visible client-side; mitigated by
  scoping the search key (allowed indices, optional rate limits / secured API
  keys) — acceptable for public catalog search.
- **Neutral:** Indexing stays server-authoritative and event-driven; the BFF
  never serves search results.

## More Information

- [Architecture plan — API surface note](../architecture-plan.md)
- [Vendor setup — Algolia](../vendor-setup.md)
