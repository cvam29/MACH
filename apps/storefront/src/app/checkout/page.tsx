import type { Metadata } from "next";

import { PageShell } from "@/components/layout/page-shell";
import { CheckoutFlow } from "@/components/checkout/checkout-flow";
import { getServerCart } from "@/lib/server/cart";

export const metadata: Metadata = { title: "Checkout" };

export default async function CheckoutPage() {
  const cart = await getServerCart();

  return (
    <PageShell
      title="Checkout"
      description="Sign in or guest, address, distance-priced delivery, then Adyen Drop-in payment."
    >
      <CheckoutFlow initialCart={cart} />
    </PageShell>
  );
}
