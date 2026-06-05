/**
 * Typed Backend-for-Frontend client.
 *
 * Wraps the shared `TypedHttpClient` and exposes one method per Bff.Functions
 * route (architecture-plan.md "API surface"). Every response is zod-validated.
 *
 * - Base URL from `NEXT_PUBLIC_BFF_URL` (default http://localhost:7071/api).
 * - Cookies forwarded (`credentials: 'include'`) so the httpOnly session/cart
 *   cookies reach customer-scoped routes (`/orders/me`, cart writes).
 * - `traceparent` + `x-correlation-id` injected/propagated per request.
 *
 * Usable from both Server Components (pass the inbound trace context) and
 * Client Components.
 */
import { z } from "zod";

import { env } from "@/lib/env";
import {
  TypedHttpClient,
  type RequestContext,
} from "@/lib/http/client";
import {
  CartSchema,
  CategoryListSchema,
  ContentEntrySchema,
  DeliveryOptionsSchema,
  HealthSchema,
  NavigationSchema,
  OrderListSchema,
  OrderSchema,
  PaymentSessionSchema,
  ProductSchema,
  SearchResultSchema,
  SearchSuggestSchema,
  type Cart,
  type Category,
  type ContentEntry,
  type DeliveryOptions,
  type DeliveryType,
  type Health,
  type Navigation,
  type Order,
  type OrderList,
  type PaymentSession,
  type Product,
  type SearchResult,
  type SearchSuggest,
} from "@/lib/bff/schemas";

export interface LineItemInput {
  sku: string;
  quantity: number;
}

export interface AddressInput {
  firstName?: string;
  lastName?: string;
  street: string;
  city: string;
  postalCode: string;
  country: string;
}

export class BffClient {
  private readonly http: TypedHttpClient;

  constructor(baseUrl: string = env.bffUrl) {
    this.http = new TypedHttpClient(baseUrl);
  }

  /* --------------------------------------------------------- catalog --- */

  /** `GET /catalog/categories` */
  getCategories(ctx: RequestContext = {}): Promise<Category[]> {
    return this.http
      .request("/catalog/categories", CategoryListSchema, {
        next: { revalidate: 300 },
        ...ctx,
      })
      .then((r) => r.categories);
  }

  /* ---------------------------------------------------------- search --- */

  /** `GET /search` */
  search(
    params: { query: string; page?: number; category?: string },
    ctx: RequestContext = {}
  ): Promise<SearchResult> {
    return this.http.request("/search", SearchResultSchema, {
      query: {
        q: params.query,
        page: params.page,
        category: params.category,
      },
      ...ctx,
    });
  }

  /** `GET /search/suggest` */
  searchSuggest(
    query: string,
    ctx: RequestContext = {}
  ): Promise<SearchSuggest> {
    return this.http.request("/search/suggest", SearchSuggestSchema, {
      query: { q: query },
      ...ctx,
    });
  }

  /* --------------------------------------------------------- product --- */

  /** `GET /products/{slug}` (commercetools + Contentstack merged). */
  getProduct(slug: string, ctx: RequestContext = {}): Promise<Product> {
    return this.http.request(
      `/products/${encodeURIComponent(slug)}`,
      ProductSchema,
      { next: { revalidate: 60 }, ...ctx }
    );
  }

  /* ------------------------------------------------------------ cart --- */

  /** `POST /carts` */
  createCart(ctx: RequestContext = {}): Promise<Cart> {
    return this.http.request("/carts", CartSchema, { method: "POST", ...ctx });
  }

  /** `GET /carts/{id}` */
  getCart(id: string, ctx: RequestContext = {}): Promise<Cart> {
    return this.http.request(
      `/carts/${encodeURIComponent(id)}`,
      CartSchema,
      ctx
    );
  }

  /** `POST /carts/{id}/line-items` */
  addLineItem(
    id: string,
    input: LineItemInput,
    opts: RequestContext & { idempotencyKey?: string } = {}
  ): Promise<Cart> {
    return this.http.request(
      `/carts/${encodeURIComponent(id)}/line-items`,
      CartSchema,
      { method: "POST", body: input, ...opts }
    );
  }

  /** `PATCH /carts/{id}/line-items` */
  updateLineItem(
    id: string,
    lineItemId: string,
    quantity: number,
    ctx: RequestContext = {}
  ): Promise<Cart> {
    return this.http.request(
      `/carts/${encodeURIComponent(id)}/line-items`,
      CartSchema,
      { method: "PATCH", body: { lineItemId, quantity }, ...ctx }
    );
  }

  /** `POST /carts/{id}/shipping` */
  setShippingAddress(
    id: string,
    address: AddressInput,
    ctx: RequestContext = {}
  ): Promise<Cart> {
    return this.http.request(
      `/carts/${encodeURIComponent(id)}/shipping`,
      CartSchema,
      { method: "POST", body: address, ...ctx }
    );
  }

  /* ------------------------------------------------ delivery options --- */

  /** `POST /carts/{id}/delivery-options` (Azure Maps distance -> quotes). */
  getDeliveryOptions(
    id: string,
    address: AddressInput,
    ctx: RequestContext = {}
  ): Promise<DeliveryOptions> {
    return this.http.request(
      `/carts/${encodeURIComponent(id)}/delivery-options`,
      DeliveryOptionsSchema,
      { method: "POST", body: address, ...ctx }
    );
  }

  /** `PUT /carts/{id}/delivery` (select a delivery type). */
  selectDelivery(
    id: string,
    type: DeliveryType,
    ctx: RequestContext = {}
  ): Promise<Cart> {
    return this.http.request(
      `/carts/${encodeURIComponent(id)}/delivery`,
      CartSchema,
      { method: "PUT", body: { type }, ...ctx }
    );
  }

  /* -------------------------------------------------------- checkout --- */

  /** `POST /checkout/{cartId}/payment-session` (commercetools + Adyen). */
  createPaymentSession(
    cartId: string,
    opts: RequestContext & { idempotencyKey?: string } = {}
  ): Promise<PaymentSession> {
    return this.http.request(
      `/checkout/${encodeURIComponent(cartId)}/payment-session`,
      PaymentSessionSchema,
      { method: "POST", ...opts }
    );
  }

  /** `POST /checkout/{cartId}/order` */
  placeOrder(
    cartId: string,
    opts: RequestContext & { idempotencyKey?: string } = {}
  ): Promise<Order> {
    return this.http.request(
      `/checkout/${encodeURIComponent(cartId)}/order`,
      OrderSchema,
      { method: "POST", ...opts }
    );
  }

  /* ---------------------------------------------------------- orders --- */

  /** `GET /orders/me` (SQL projection, requires session cookie). */
  getMyOrders(ctx: RequestContext = {}): Promise<OrderList> {
    return this.http.request("/orders/me", OrderListSchema, {
      cache: "no-store",
      ...ctx,
    });
  }

  /** `GET /orders/{id}` */
  getOrder(id: string, ctx: RequestContext = {}): Promise<Order> {
    return this.http.request(
      `/orders/${encodeURIComponent(id)}`,
      OrderSchema,
      { cache: "no-store", ...ctx }
    );
  }

  /* --------------------------------------------------------- content --- */

  /** `GET /content/navigation` */
  getNavigation(ctx: RequestContext = {}): Promise<Navigation> {
    return this.http.request("/content/navigation", NavigationSchema, {
      next: { revalidate: 300 },
      ...ctx,
    });
  }

  /** `GET /content/{type}/{slug}` */
  getContent(
    type: string,
    slug: string,
    ctx: RequestContext = {}
  ): Promise<ContentEntry> {
    return this.http.request(
      `/content/${encodeURIComponent(type)}/${encodeURIComponent(slug)}`,
      ContentEntrySchema,
      { next: { revalidate: 300 }, ...ctx }
    );
  }

  /* ---------------------------------------------------------- health --- */

  /** `GET /health` */
  health(ctx: RequestContext = {}): Promise<Health> {
    return this.http.request("/health", HealthSchema, {
      cache: "no-store",
      ...ctx,
    });
  }
}

/** Default singleton against the configured BFF base URL. */
export const bff = new BffClient();

// Re-export the schema namespace marker so consumers can import types from one
// place if they prefer. Concrete types live in `./schemas`.
export type { z };
