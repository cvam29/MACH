import "server-only";

import {
  OrderListSchema,
  OrderSchema,
  type Order,
} from "@/lib/bff/schemas";
import { serverGet } from "@/lib/server/bff";

/**
 * Order history for the signed-in customer (`GET /orders/me`, SQL projection).
 * Forwards the session cookie. Returns an empty list when offline or empty.
 */
export async function getMyOrders(): Promise<Order[]> {
  const result = await serverGet("/orders/me", OrderListSchema, {
    withCookies: true,
  });
  return result?.orders ?? [];
}

/** A single order by id (`GET /orders/{id}`). */
export async function getOrderById(id: string): Promise<Order | null> {
  return serverGet(`/orders/${encodeURIComponent(id)}`, OrderSchema, {
    withCookies: true,
  });
}
