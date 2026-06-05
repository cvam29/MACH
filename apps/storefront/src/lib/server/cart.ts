import "server-only";

import { cookies } from "next/headers";

import { CartSchema, type Cart } from "@/lib/bff/schemas";
import { serverGet } from "@/lib/server/bff";

/**
 * Name of the httpOnly cookie the BFF sets to mirror the commercetools cart id
 * on the storefront. The cart itself stays server-authoritative; the storefront
 * only ever holds this id (+ version in the Zustand mirror). Kept here as the
 * single source of truth so server reads and the BFF agree.
 */
export const CART_ID_COOKIE = "mach_cart";

/**
 * Read the current authoritative cart server-side, if a cart cookie exists.
 * Forwards the inbound cookies so the BFF can authorize the read. Returns `null`
 * when there is no cart yet or the BFF is unreachable (never throws).
 */
export async function getServerCart(): Promise<Cart | null> {
  const store = await cookies();
  const cartId = store.get(CART_ID_COOKIE)?.value;
  if (!cartId) return null;

  return serverGet(`/carts/${encodeURIComponent(cartId)}`, CartSchema, {
    withCookies: true,
  });
}
