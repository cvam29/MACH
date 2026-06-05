# 0010 — OpenTelemetry for end-to-end tracing

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** observability, architecture

## Context and Problem Statement

A composable system spans a storefront, APIM, multiple Functions hosts, several
vendor calls, and async Service Bus flows. Debugging and demonstrating "where did
this request go" requires correlation across all of them. What instrumentation
approach do we standardize on?

## Decision Drivers

- **End-to-end correlation** across sync + async hops, including vendor spans.
- Vendor-neutral, standards-based, future-proof instrumentation.
- One reusable wiring shared by every Functions host.
- A compelling visual: App Insights Application Map fanning to all vendors.

## Considered Options

1. **App Insights classic SDK only** — proprietary, per-host wiring.
2. **Ad-hoc logging + correlation ids** — manual, brittle, no dependency spans.
3. **OpenTelemetry (traces/metrics/logs) exported to Azure Monitor / App Insights**
   via a shared `Mach.ServiceDefaults`.

## Decision Outcome

**Chosen option: "OpenTelemetry, exported to App Insights."** `Mach.ServiceDefaults`
wires OTel traces/metrics/logs for every host (`Azure.Monitor.OpenTelemetry...`).
The **W3C `traceparent`** propagates storefront → APIM → BFF → vendor dependency
spans → Service Bus and back, with an `x-correlation-id` carried on HTTP headers
and Service Bus application properties (see the
[checkout sequence](../diagrams/checkout-sequence.md)). Per-vendor health checks
and custom metrics (reindex lag, webhook latency, cart conversion) round it out.

## Consequences

- **Good:** One trace stitches a shopper action across all sync + async hops and
  all vendors — the App Insights Application Map is the Cloud-native punchline.
- **Good:** Vendor-neutral, standards-based; the exporter is swappable.
- **Good:** Consistent instrumentation via a single shared project — no per-host
  drift.
- **Bad / trade-off:** Telemetry overhead and some sampling/config tuning;
  negligible at demo scale.
- **Neutral:** Locally, traces/logs go to console/OTel without needing App
  Insights, so observability works offline.

## More Information

- [Architecture plan — Observability](../architecture-plan.md)
- [Checkout sequence — correlation ids](../diagrams/checkout-sequence.md)
