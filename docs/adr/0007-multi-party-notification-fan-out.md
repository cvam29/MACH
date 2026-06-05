# 0007 — Multi-party notification fan-out (customer / store / supplier / reception)

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** messaging, notifications, event-driven

## Context and Problem Statement

A placed/paid order should notify several **different audiences**, each with its
own message and recipient: the **customer** (confirmation), the **store** (new
order to fulfil), the **supplier** (replenishment/dropship for the SKUs), and
**reception** (goods-in / pickup-ready). How do we deliver four distinct,
correctly-addressed, copy-managed emails reliably and exactly once each?

## Decision Drivers

- Event-driven, decoupled from the synchronous checkout path.
- Each audience: distinct template + recipient; copy owned by content, not code.
- **Exactly one** email per audience per order (idempotent).
- Works offline for demos.

## Considered Options

1. **Inline send in the checkout request** — BFF emails everyone synchronously.
2. **Single generic email** to one recipient list.
3. **Async fan-out worker** — order events → Service Bus `notifications` topic →
   `Notifications.Functions` resolves four audiences and sends a templated email
   per audience, idempotently.

## Decision Outcome

**Chosen option: "Async fan-out worker."** Order-placed / payment events publish
to the Service Bus `notifications` topic (via the transactional outbox).
`Notifications.Functions` resolves **four audiences** and sends a distinct
Contentstack-templated email to each:

- **Customer** — order confirmation (recipient from the order).
- **Store** — new order to fulfil (assigned/nearest store from `fulfillment.Stores`).
- **Supplier** — replenishment/dropship request (resolved per SKU via
  `fulfillment.ProductSuppliers` → `fulfillment.Suppliers`).
- **Reception** — goods-in / pickup-ready notice (store reception email).

Copy comes from **Contentstack** (one Email-Template entry per audience,
personalized with `{{order}}` / `{{delivery}}` tokens); recipients resolve from
SQL with `Notifications:*` config fallbacks; delivery is via **ACS Email** (or
the `DevSink` `.eml` writer offline). Each send is recorded in
`notifications.EmailDeliveries` keyed by **`OrderId+Audience+Kind`** (unique),
guaranteeing exactly one per audience.

## Consequences

- **Good:** Checkout latency is unaffected; notification failures don't fail the
  order; retries are safe (idempotency key per audience).
- **Good:** Copy is editable by non-engineers in Contentstack; recipients are
  data-driven from the fulfillment tables.
- **Good:** Strong portfolio signal — visible "event-driven async" proof: four
  `.eml` files land in `./mail/` offline.
- **Bad / trade-off:** More infrastructure (topic, worker, audit table) than an
  inline send; justified by reliability and decoupling.
- **Neutral:** Definitive payment state still flows via the Adyen webhook →
  projection path, which can also trigger payment-authorized/failed notifications.

## More Information

- [Architecture plan — Email / notifications fan-out](../architecture-plan.md)
- [Checkout sequence — fan-out](../diagrams/checkout-sequence.md)
- [Context map — EmailDeliveries / recipients](../diagrams/context-map.md)
