# 0011 — Redis cache-aside with event-driven invalidation

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** performance, caching, architecture

## Context and Problem Statement

Hot read paths — product detail (PDP), category/listing trees, navigation, CMS content, and
delivery quotes — repeatedly call commercetools, Contentstack, and Azure Maps. Those vendor calls
add latency and count against rate limits. We want to accelerate reads without making the cache a
second source of truth, and without serving stale content after an editor or merchandiser publishes
a change. How do we cache?

## Decision Drivers

- **Lower read latency** and fewer vendor round-trips on hot paths.
- **SQL/vendors stay authoritative** — the cache must be disposable and rebuildable.
- **Freshness:** a product/content change must promptly evict affected entries.
- **Offline-friendly:** the demo must run with no Redis and no behavior change.
- **Vendor-neutral seam** so the cache technology is swappable.

## Considered Options

1. **No cache** — simplest, but every read hits a vendor; latency and rate-limit exposure.
2. **In-process `MemoryCache` only** — fast, but per-host, not shared, and hard to invalidate
   across hosts on a change event.
3. **Distributed cache-aside behind an `ICacheStore` port**, backed by **Azure Cache for Redis**
   (StackExchange.Redis) in the cloud and an in-memory implementation offline, with **event-driven
   invalidation** by the Indexer on change events.

## Decision Outcome

**Chosen option: "Distributed cache-aside behind `ICacheStore`."**

- The BFF reads through `ICacheStore.GetOrSetAsync(key, factory, ttl, ct)` — return the cached value
  or invoke the vendor, cache the result for a TTL (default 300s), and return it.
- Keys are namespaced under logical prefixes — `product:{slug}`, `catalog:{…}`,
  `content:{type}:{slug}` (see `CachePrefixes`).
- **Invalidation is event-driven, not TTL-only.** commercetools/Contentstack change webhooks →
  Service Bus → the **Indexer** host calls `RemoveByPrefixAsync(...)` so the BFF re-populates from
  source on the next request. This keeps the cache and the search index converging from the *same*
  change events.
- The provider is selected by `Cache:Provider` (`InMemory` default, `Redis`); Redis adds
  `Cache:ConnectionString` + `Cache:InstanceName` (default `Mach`). **SQL remains the source of
  truth** for durable data (outbox/inbox/projections); the cache is purely an accelerator.

## Consequences

- **Good:** Lower latency and fewer vendor calls on the hottest paths; the same change events drive
  both cache invalidation and reindexing — one coherent freshness story.
- **Good:** The `ICacheStore` seam keeps Redis out of the application/domain; swapping the cache
  backend touches one Translator project.
- **Good:** Runs fully offline with `Cache:Provider=InMemory` — no Redis required for the demo.
- **Bad / trade-off:** A small staleness window between a change and its invalidation (bounded by
  webhook + Service Bus latency, then TTL as a backstop). Acceptable for catalog/content reads;
  prices/availability that must be exact are read live at checkout.
- **Bad / trade-off:** Prefix invalidation is coarse (evicts a whole prefix, not one entry) — simple
  and safe, at the cost of some avoidable cache misses after a change.
- **Neutral:** Cache-key prefixes are a **convention shared** between the BFF (writer) and the
  Indexer (evictor); `CachePrefixes` documents the contract and must stay in sync.

## More Information

- [Architecture plan — Caching](../architecture-plan.md)
- [ADR 0010 — OpenTelemetry end-to-end tracing](./0010-opentelemetry-end-to-end-tracing.md)
- `Mach.Application/Ports/ICacheStore.cs`, `Mach.Indexer.Functions/Indexing/CacheInvalidator.cs`
