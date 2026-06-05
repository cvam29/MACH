# 0006 — Distance-based delivery with external shipping price on the commercetools cart

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** commerce, delivery, integration

## Context and Problem Statement

Delivery options and prices should depend on **how far** the shipping address is
from the fulfilling store — including a **same-day** option gated by distance and
a free **store-pickup** option. commercetools shipping methods alone can't
express live, per-address, distance-derived prices. How do we compute and apply
delivery pricing?

## Decision Drivers

- Realistic, demonstrable geo feature (showcases Azure Maps + composition).
- Prices/ETAs must reflect the *actual* address ↔ store distance at checkout.
- The cart total must remain commercetools-authoritative (tax, totals, order).
- Delivery types: **standard / express / same-day / store-pickup**.

## Considered Options

1. **Static shipping methods only** — fixed zones/prices in commercetools.
2. **Distance computed in the BFF via Azure Maps; set the chosen option back on
   the cart as the shipping method + an external price.**
3. **Third-party shipping-rate vendor** integrated separately.

## Decision Outcome

**Chosen option: "Distance computed in the BFF; external price on the cart."**
At checkout the BFF geocodes the shipping address via **Azure Maps**, computes
distance to the nearest fulfilling **Store** (from SQL `fulfillment.Stores`), and
returns **quotes per type** — standard/express priced as `base + perKm·distance`
with type-specific ETAs; **same-day** offered only inside a distance threshold;
**store-pickup** free. The selected type is written back to the commercetools
cart as the shipping method with an **external shipping price**, so commercetools
still owns totals, tax, and the resulting order.

## Consequences

- **Good:** Live, address-accurate delivery prices/ETAs; same-day correctly
  gated by distance; pickup map of nearby stores.
- **Good:** commercetools remains authoritative for cart totals and the order —
  the external price is just the computed rate handed to the engine.
- **Good:** Works fully offline via the Maps **haversine stub** over seeded store
  coordinates (`MAPS_PROVIDER=Stub`).
- **Bad / trade-off:** Adds an Azure Maps dependency on the checkout path and a
  geocode/matrix call; mitigated by Polly resilience, caching, and the stub.
- **Neutral:** Requires seeded Stores with lat/lng spread across a region so
  distance visibly varies.

## More Information

- [Architecture plan — Delivery quoting](../architecture-plan.md)
- [Checkout sequence — delivery options](../diagrams/checkout-sequence.md)
- [Vendor setup — Azure Maps](../vendor-setup.md)
