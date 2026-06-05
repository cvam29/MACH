# Vendor Sandbox Setup

Step-by-step creation of every external sandbox account this demo needs, and how
each credential maps to a variable in [`../.env.example`](../.env.example).

All vendors here have **free sandbox / test tiers**. Nothing in this guide
incurs cost. After collecting credentials, copy `.env.example` → `.env` (and/or
mirror secrets into .NET user-secrets and a gitignored `local.settings.json` per
Functions host — secrets must never be committed).

**Credential markers:** `[PUBLIC]` values may ship to the browser; `[SECRET]`
values stay server-side (Key Vault in prod, user-secrets locally).

---

## 1. commercetools — commerce engine + customer identity

commercetools is both the commerce engine **and** the identity provider (the
Auth microservice wraps its customer authentication — see
[ADR 0003](adr/0003-commercetools-customer-auth-identity-provider.md)).

1. Sign up for a free trial at <https://commercetools.com/free-trial> and create
   an **organization** and a **project**. Note the **project key** and the
   **region** (e.g. `europe-west1.gcp`) shown in the Merchant Center URL.
2. In Merchant Center → **Settings → Developer settings → API clients**, create
   an API client. For a demo, the **Admin client** template is simplest; for a
   tighter posture create a custom client with at least these scopes:
   - `view_products`, `view_categories`
   - `manage_orders`, `view_orders`
   - `manage_customers`, `view_customers`
   - **Customer-scoped (`/me`) scopes** — required for the Auth flow and
     customer-authoritative cart/profile/order work:
     `view_published_products:{key}`, `manage_my_orders:{key}`,
     `manage_my_profile:{key}`, `manage_my_shopping_lists:{key}`,
     `create_anonymous_token:{key}` (anonymous sessions),
     `manage_my_payments:{key}`.
3. **Copy the secret immediately** — commercetools shows the client secret only
   once. The client-credentials block lists the **Auth URL** and **API URL** for
   your region; copy both.
4. **Password flow / anonymous notes:** the Auth microservice exchanges customer
   email+password at `{CT_AUTH_URL}/oauth/{projectKey}/customers/token` (OAuth2
   **password grant**) to obtain a *customer* access + refresh token, and mints
   guest sessions at `.../anonymous/token`. Customer tokens are scoped to `/me`
   endpoints. Ensure `create_anonymous_token` and the `manage_my_*` scopes are
   present or guest checkout and `/me` calls will 403.

| Portal value | `.env.example` variable | Marker |
|---|---|---|
| Project key | `CT_PROJECT_KEY` | PUBLIC |
| Region/provider | `CT_REGION` | PUBLIC |
| Auth URL | `CT_AUTH_URL` | PUBLIC |
| API URL | `CT_API_URL` | PUBLIC |
| API client id | `CT_CLIENT_ID` | SECRET |
| API client secret | `CT_CLIENT_SECRET` | SECRET |
| Scopes (space-delimited) | `CT_SCOPES` | SECRET |

---

## 2. Contentstack — content + email copy

1. Sign up at <https://www.contentstack.com/> (free developer tier) and create a
   **Stack**. Note your **region** (US / EU / Azure-NA …) — it determines the
   delivery & management API hosts.
2. **Settings → Tokens → Delivery Tokens:** create a token scoped to your
   delivery **environment** (e.g. `development`). Copy the **delivery token** and
   the environment name. The **stack API key** is on the same screen / stack
   settings.
3. **Settings → Tokens → Management Tokens:** create a management token (used
   only by seed scripts to create content types and entries — keep server-side).
4. **Email-template content type:** the seed scripts create an **Email Template**
   content type with fields `audience` (customer/store/supplier/reception),
   `kind`, `subject`, and `body` (rich text with `{{order}}` / `{{delivery}}`
   tokens), plus one entry per audience. You can also create it manually under
   **Content Models** if you prefer to inspect it first. Publish entries to the
   delivery environment so the delivery token can read them.

| Portal value | `.env.example` variable | Marker |
|---|---|---|
| Stack API key | `CONTENTSTACK_API_KEY` | PUBLIC |
| Delivery token | `CONTENTSTACK_DELIVERY_TOKEN` | PUBLIC |
| Environment name | `CONTENTSTACK_ENVIRONMENT` | PUBLIC |
| Region | `CONTENTSTACK_REGION` | PUBLIC |
| Management token | `CONTENTSTACK_MANAGEMENT_TOKEN` | SECRET |

---

## 3. Adyen — payments (test mode)

1. Sign up for a **test** account at <https://www.adyen.com/signup> and open the
   **Customer Area** (test environment). Create a **merchant account** (ECOM) if
   one does not already exist; note its name.
2. **Developers → API credentials:** open the web-service user. Generate/copy:
   - **API key** `[SECRET]` — server-side payment requests.
   - **Client key** `[PUBLIC]` — used by the Adyen Web Drop-in in the browser.
3. **Allowed origins:** on the same API-credential screen, add your storefront
   origin (e.g. `http://localhost:3000`). The client key will not initialize in
   the browser from an origin that is not allow-listed.
4. **Webhooks (HMAC):** **Developers → Webhooks → Add → Standard webhook.** Set
   the URL to your webhook host (`/hooks/adyen`; locally via a dev tunnel/ngrok),
   then **Generate the HMAC key** and copy it. `Webhooks.Functions` verifies this
   HMAC before accepting a notification. For fully offline demos, use the bundled
   "replay sample notification" script instead of a live tunnel.
5. **Test cards:** use Adyen's test card numbers (e.g. `4111 1111 1111 1111`)
   with a 3DS-triggering card to exercise the challenge flow.

| Portal value | `.env.example` variable | Marker |
|---|---|---|
| Client key | `ADYEN_CLIENT_KEY` | PUBLIC |
| Environment (`test`) | `ADYEN_ENVIRONMENT` | PUBLIC |
| Merchant account | `ADYEN_MERCHANT_ACCOUNT` | PUBLIC |
| API key | `ADYEN_API_KEY` | SECRET |
| Webhook HMAC key | `ADYEN_HMAC_KEY` | SECRET |
| Allowed origins | _(portal setting, no env var)_ | — |

---

## 4. Algolia — search

Search runs **browser-side** with a search-only key; the BFF/indexer own writes
(see [ADR 0004](adr/0004-algolia-browser-search-key.md)).

1. Sign up at <https://www.algolia.com/> (free tier) and create an
   **Application**. Note the **Application ID**.
2. **Settings → API Keys:**
   - **Search-Only API Key** `[PUBLIC]` — shipped to the storefront bundle.
   - **Admin API Key** `[SECRET]` — used only by `Indexer.Functions` and the
     seed script to create/replace records and index settings. Never ship it.
3. The seed script creates the `products` index and configures
   `searchableAttributes`, `attributesForFaceting`, `customRanking`, and
   Query Suggestions. You can pre-create an empty `products` index in the
   dashboard if you prefer.

| Portal value | `.env.example` variable | Marker |
|---|---|---|
| Application ID | `ALGOLIA_APP_ID` | PUBLIC |
| Search-only key | `ALGOLIA_SEARCH_KEY` | PUBLIC |
| Index name | `ALGOLIA_INDEX_NAME` | PUBLIC |
| Admin key | `ALGOLIA_ADMIN_KEY` | SECRET |

---

## 5. Azure Maps — geocoding + distance/route matrix

1. In the **Azure portal**, create an **Azure Maps account** (the **Gen2 (S0)**
   tier has a generous free monthly grant — geocoding + route/matrix calls for
   this demo stay well within it).
2. **Authentication → Shared Key authentication:** copy the **Primary key**
   `[SECRET]`. The Maps adapter uses it server-side for geocode + distance.
3. **Offline alternative:** no Azure account needed — set `MAPS_PROVIDER=Stub`
   to use a haversine computation over the seeded store coordinates, so
   distance-based delivery works fully offline.

| Portal value | `.env.example` variable | Marker |
|---|---|---|
| Primary key | `AZURE_MAPS_KEY` | SECRET |
| Provider toggle | `MAPS_PROVIDER` (`AzureMaps`/`Stub`) | PUBLIC |

---

## 6. ACS Email — transactional email (or dev sink)

1. In the **Azure portal**, create a **Communication Services** resource, then an
   **Email Communication Service** with a domain — the **Azure-managed domain**
   (`*.azurecomm.net`) is the zero-config option and provisions a verified
   sender address automatically.
2. **Connect** the Email service to the Communication Services resource, then in
   the Communication Services resource → **Keys**, copy the **connection string**
   `[SECRET]`. Copy the managed-domain **MailFrom / sender address** `[PUBLIC]`.
3. **Offline alternative:** no Azure account needed — set `EMAIL_PROVIDER=DevSink`
   to write each outbound email as an `.eml` file into `./mail/`. The four
   audience emails (customer/store/supplier/reception) all land there for offline
   demos.

| Portal value | `.env.example` variable | Marker |
|---|---|---|
| ACS connection string | `ACS_CONNECTION_STRING` | SECRET |
| Sender (MailFrom) address | `ACS_SENDER_ADDRESS` | PUBLIC |
| Provider toggle | `EMAIL_PROVIDER` (`Acs`/`DevSink`) | PUBLIC |

### Notification recipients (fan-out)

The multi-party fan-out (see
[ADR 0007](adr/0007-multi-party-notification-fan-out.md)) resolves recipients
from SQL (`fulfillment.Stores`, `fulfillment.Suppliers`) at runtime; the
`NOTIFICATIONS_RECIPIENT_*` variables are **config fallbacks** used when no
SQL row applies (e.g. before seeding):

| Audience | `.env.example` variable | Marker |
|---|---|---|
| Customer | `NOTIFICATIONS_RECIPIENT_CUSTOMER` | PUBLIC |
| Store | `NOTIFICATIONS_RECIPIENT_STORE` | PUBLIC |
| Supplier | `NOTIFICATIONS_RECIPIENT_SUPPLIER` | PUBLIC |
| Reception | `NOTIFICATIONS_RECIPIENT_RECEPTION` | PUBLIC |

---

## 7. Azure platform (Service Bus, Storage, SQL) — connection strings

These are **not third-party vendors**; for local runs they have offline modes,
so you can defer real Azure resources entirely.

| Concern | `.env.example` variable | Marker | Offline mode |
|---|---|---|---|
| Service Bus | `SERVICEBUS_CONNECTION_STRING` | SECRET | `MESSAGING_PROVIDER=InMemory` (or emulator) |
| Storage (Functions runtime) | `AZURE_WEBJOBS_STORAGE` | SECRET | `UseDevelopmentStorage=true` (Azurite) |
| SQL Server | `SQL_CONNECTION_STRING` | SECRET | `(localdb)\MSSQLLocalDB` |

---

## Public vs secret — at a glance

**`[PUBLIC]`** (safe in the browser bundle): `CT_PROJECT_KEY`, `CT_REGION`,
`CT_AUTH_URL`, `CT_API_URL`, `CONTENTSTACK_API_KEY`,
`CONTENTSTACK_DELIVERY_TOKEN`, `CONTENTSTACK_ENVIRONMENT`,
`CONTENTSTACK_REGION`, `ADYEN_CLIENT_KEY`, `ADYEN_ENVIRONMENT`,
`ADYEN_MERCHANT_ACCOUNT`, `ALGOLIA_APP_ID`, `ALGOLIA_SEARCH_KEY`,
`ALGOLIA_INDEX_NAME`, `ACS_SENDER_ADDRESS`, the `*_PROVIDER` toggles, the
`NOTIFICATIONS_RECIPIENT_*` fallbacks, and the `NEXT_PUBLIC_*` URLs.

**`[SECRET]`** (server-side only — Key Vault / user-secrets): `CT_CLIENT_ID`,
`CT_CLIENT_SECRET`, `CT_SCOPES`, `CONTENTSTACK_MANAGEMENT_TOKEN`,
`ADYEN_API_KEY`, `ADYEN_HMAC_KEY`, `ALGOLIA_ADMIN_KEY`, `AZURE_MAPS_KEY`,
`ACS_CONNECTION_STRING`, `SERVICEBUS_CONNECTION_STRING`, `AZURE_WEBJOBS_STORAGE`,
`SQL_CONNECTION_STRING`.

> The commercetools API-client secret, Adyen API key/HMAC, Algolia admin key,
> Azure Maps key, and ACS connection string in particular must **never** appear
> in client code, Terraform state, or committed config. In production they are
> Key Vault secrets resolved via managed identity; locally they live in .NET
> user-secrets or a gitignored `local.settings.json`.
