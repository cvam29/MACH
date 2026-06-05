# Demo script — a 3–5 minute MACH walkthrough

A guided tour that maps each step to one letter of **MACH** (Microservices, API-first, Cloud-native,
Headless). Run `./run.ps1` first (see [run-local.md](./run-local.md)) and seed data if you have vendor
keys; the offline providers keep every step working without them.

> **Setup:** storefront on <http://localhost:3000>, all hosts up, `./mail` empty, a browser dev-tools
> Network tab open. Total time ~4 minutes.

---

## 0. The pitch (15s)

> "This is a composable commerce storefront. No monolith — it's a Next.js head over seven independent
> .NET microservices, each owning one capability and one vendor. Best-of-breed SaaS for commerce,
> content, search, and payments; SQL only as the reliability backbone. Everything you'll see runs
> locally with no cloud cost."

Show the container diagram in the [README](../README.md#container-diagram-c4) for one beat.

---

## 1. Headless — the home page is content, not code (30s)

Open <http://localhost:3000>.

- The hero, nav, and footer are **Contentstack** entries delivered headless; the featured products
  are **commercetools**. The same page composes two vendors.
- **Point at:** in the Network tab, `GET /api/content/navigation` and the home payload — content and
  commerce arrive from different systems and are merged by the BFF.

**→ Headless.**

---

## 2. API-first + Microservices — search and a composed PDP (45s)

Use the search box (type e.g. "jacket").

- Autocomplete and results come from **Algolia**, queried **browser-side** with a search-only key —
  the canonical pattern (see [ADR 0004](./adr/0004-algolia-browser-search-key.md)). The BFF owns only
  indexing, done by the **Indexer** microservice off catalog/content change events.
- Click a product. The PDP is the **composability punchline**: one card shows commercetools
  price/availability **+** Algolia discoverability **+** Contentstack editorial, simultaneously.
- **Point at:** `GET /api/products/{slug}` — the BFF fan-joins commercetools and Contentstack in one
  response.

**→ API-first, Microservices.**

---

## 3. Identity — sign in through the Auth microservice (30s)

Click **Sign in**, register or log in.

- Auth is a dedicated microservice (`:7070`) wrapping **commercetools customer authentication**
  (OAuth2 password flow). Tokens are returned **only as httpOnly + Secure + SameSite cookies** — never
  localStorage (see [ADR 0003](./adr/0003-commercetools-customer-auth-identity-provider.md)).
- The guest's **anonymous cart merges** into the customer cart on sign-in.
- **Point at:** the `Set-Cookie` on `POST /api/auth/login` in the Network tab — flagged HttpOnly,
  and the JS console can't read it.

**→ Microservices (no separate IdP).**

---

## 4. Distance-based delivery — the same address, re-priced live (45s)

Add the product to the cart, go to checkout, enter a shipping address.

- The BFF geocodes the address via **Azure Maps** (or the haversine stub offline), measures distance
  to the nearest seeded **Store**, and returns **delivery options per type**: standard / express
  priced `base + perKm·distance`, **same-day** offered only inside a distance threshold, **store-pickup**
  free (see [ADR 0006](./adr/0006-distance-based-delivery-external-shipping-price.md)).
- **Do the reveal:** change to a far-away address and watch prices/ETAs change and **same-day disappear**.
- The chosen option is written back to the commercetools cart as the shipping method + external price.
- **Point at:** `POST /api/carts/{id}/delivery-options` request/response.

**→ API-first (real business logic behind a clean contract).**

---

## 5. Payments — Adyen checkout, then async fan-out (45s)

Pay with an **Adyen** test card (e.g. `4212 3456 7890 1237`, any future expiry / `737`); trigger 3DS.

- Checkout creates a commercetools order + an Adyen session. Adyen confirms via a **signed webhook**
  to the Webhooks host (`:7072`), which verifies the HMAC, dedupes into the inbox, and publishes
  `payment.notification.received` to Service Bus — **then** ACKs fast with `[accepted]`.
- The **Projection** worker (`:7073`) consumes it, transitions the order to *Paid*, and upserts the
  `/orders/me` read model. The **Notifications** worker (`:7075`) fans out.
- *Offline / no Adyen keys?* Run the bundled replay script to post a sample signed notification, so
  the async chain still fires.

**→ Microservices + event-driven async.**

---

## 6. Multi-party notifications — four emails, copy from the CMS (30s)

Open the `./mail` folder.

- **Four `.eml` files** appear — one each for **Customer** (confirmation), **Store** (new order to
  fulfil), **Supplier** (replenishment for the SKUs), **Reception** (goods-in) — every audience its own
  template and recipient (see [ADR 0007](./adr/0007-multi-party-notification-fan-out.md)).
- Each body's **copy is authored in Contentstack**, personalized with order + delivery data. Recipients
  resolve from the SQL `Stores`/`Suppliers` seed. Sends are idempotent — exactly one per audience.

**→ Headless again (content drives transactional email), event-driven.**

---

## 7. Order history — the CQRS read model (15s)

Go to **Account → Orders** (or `GET /api/orders/me`).

- This is served from the SQL `OrderProjections` read model the Projection worker built — rebuildable
  from events, never the write path.

**→ Microservices (CQRS).**

---

## 8. Cloud-native — the platform story (30s)

No deploy needed; show the artifacts:

- **Observability:** the console traces (or App Insights Application Map when a connection string is
  set) show one `traceparent` propagating storefront → BFF → vendor spans → Service Bus. See the
  [App Insights workbook + KQL](./observability/) for reindex-lag / webhook-latency queries.
- **IaC:** `terraform -chdir=infra/terraform/environments/dev plan` is green — the full Azure topology
  as documentation, never applied (see [ADR 0009](./adr/0009-terraform-as-documentation.md)).
- **CI/CD:** the GitHub Actions run — build + tests + `terraform plan` comment + security scans — with
  `deploy.yml` gated behind OIDC and a manual approval (see [ADR 0008](./adr/0008-oidc-no-long-lived-cloud-secrets-ci.md)).

**→ Cloud-native.**

---

## Close (15s)

> "Four best-of-breed vendors, seven independently deployable services, one composed experience —
> and swapping any vendor touches exactly one folder. That's the composable promise, demonstrated end
> to end, for zero cloud spend."

Point at the dependency rule: each vendor SDK is sealed in its own Translator project, enforced by the
`Mach.Architecture.Tests` dependency test.

---

### Cheat sheet

| Step | Letter | URL / artifact |
|---|---|---|
| Home | Headless | `:3000`, `GET /api/content/navigation` |
| Search → PDP | API-first, Microservices | Algolia box, `GET /api/products/{slug}` |
| Sign in | Microservices | `POST /api/auth/login` (httpOnly cookie) |
| Delivery quote | API-first | `POST /api/carts/{id}/delivery-options` |
| Pay (Adyen) | Microservices, async | `POST /api/checkout/{cartId}/order`, webhook `:7072` |
| Four emails | Headless, async | `./mail/*.eml` |
| Orders | CQRS | `GET /api/orders/me` |
| Platform | Cloud-native | traces, `terraform plan`, Actions run |
