# Checkout Sequence

End-to-end checkout for the MACH demo: anonymous/authenticated session → cart →
distance-based delivery quote → Adyen payment + 3DS → order creation → async
multi-party notification fan-out.

**Correlation:** every hop carries the W3C `traceparent` header plus an
`x-correlation-id` (abbreviated `cid` below). The storefront's typed BFF client
injects them; APIM and the Functions hosts propagate them; the `cid` is attached
to the Service Bus message application properties so the async notification spans
stitch back to the originating request in App Insights.

```mermaid
sequenceDiagram
    autonumber
    actor U as Shopper (Browser)
    participant SF as Storefront
    participant AU as Auth Functions
    participant BFF as BFF Functions
    participant CT as commercetools
    participant MAP as Azure Maps
    participant AD as Adyen
    participant SB as Service Bus
    participant NT as Notifications Functions
    participant ACS as ACS Email

    Note over U,ACS: All calls carry W3C traceparent + x-correlation-id (cid)

    U->>AU: POST /auth/login or /auth/anonymous (cid)
    AU->>CT: OAuth2 password / anonymous session
    CT-->>AU: access + refresh tokens
    AU-->>U: Set httpOnly+Secure cookies (cid)

    U->>BFF: PATCH /carts/{id}/line-items (cookie, cid)
    BFF->>CT: /me cart update (version)
    CT-->>BFF: cart vN
    BFF-->>U: cart state (cid)

    U->>BFF: POST /carts/{id}/delivery-options (address, cid)
    BFF->>MAP: geocode + distance to nearest store
    MAP-->>BFF: distance + ETA matrix
    BFF-->>U: quotes per type (standard/express/same-day/pickup) (cid)

    U->>BFF: PUT /carts/{id}/delivery (chosen type, cid)
    BFF->>CT: set shipping method + external price
    CT-->>BFF: cart vN+1

    U->>BFF: POST /checkout/{cartId}/payment-session (cid)
    BFF->>AD: create payment session
    AD-->>BFF: session data
    BFF-->>U: Adyen session (cid)

    U->>AD: Drop-in submit (3DS challenge)
    AD-->>U: 3DS result authorised

    U->>BFF: POST /checkout/{cartId}/order (cid)
    BFF->>CT: create order from cart
    CT-->>BFF: order created
    BFF->>SB: publish OrderPlaced (via outbox, cid)
    BFF-->>U: order confirmation (cid)

    SB->>NT: OrderPlaced (cid propagated)
    NT->>CT: fetch order + delivery
    NT->>ACS: send Customer email
    NT->>ACS: send Store email
    NT->>ACS: send Supplier email
    NT->>ACS: send Reception email
    Note over NT,ACS: idempotent per OrderId+Audience+Kind
```

## Notes

- **Session first.** Guests get an *anonymous* commercetools session (so they
  have a cart); on later sign-in the anonymous cart is **merged** into the
  customer cart. Tokens never reach the browser as JS-readable values — only
  `httpOnly + Secure + SameSite` cookies. See
  [ADR 0003](../adr/0003-commercetools-customer-auth-identity-provider.md).
- **Delivery quoting is synchronous.** The BFF geocodes the address and computes
  distance to the nearest fulfilling store via Azure Maps, returning a price/ETA
  per delivery type; the chosen type is written back to the cart as the shipping
  method + **external price**. See
  [ADR 0006](../adr/0006-distance-based-delivery-external-shipping-price.md).
- **Payment authorisation.** The synchronous flow shown here creates the order
  after a successful 3DS result. The **definitive** payment state still arrives
  asynchronously via the Adyen HMAC webhook → `Webhooks.Functions` → Service Bus
  `payments` topic → `Projection.Functions` (CQRS read-model + order transition);
  that path is omitted here for focus.
- **Reliable publish.** `OrderPlaced` is appended to the transactional
  **outbox** in the same EF transaction as any local write and published by
  `Outbox.Functions` — no dual-write inconsistency.
- **Fan-out is async + idempotent.** `Notifications.Functions` resolves four
  audiences (customer / store / supplier / reception), renders Contentstack copy,
  and records each send in `EmailDeliveries` keyed by `OrderId+Audience+Kind`
  (exactly one per audience). See
  [ADR 0007](../adr/0007-multi-party-notification-fan-out.md).
