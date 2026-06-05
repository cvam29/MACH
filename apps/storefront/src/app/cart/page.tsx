import type { Metadata } from "next";

import { PageShell } from "@/components/layout/page-shell";
import { CartView } from "@/components/cart/cart-view";
import { getServerCart } from "@/lib/server/cart";

export const metadata: Metadata = { title: "Cart" };

export default async function CartPage() {
  // Server-read the authoritative cart (commercetools, via the BFF) so the page
  // is correct on first paint; the client view mirrors it into Zustand.
  const cart = await getServerCart();

  return (
    <PageShell
      title="Your cart"
      description="Server-authoritative (commercetools), mirrored in a Zustand store with optimistic quantity updates."
    >
      <CartView initialCart={cart} />
    </PageShell>
  );
}
