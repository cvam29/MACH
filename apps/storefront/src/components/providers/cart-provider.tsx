"use client";

import * as React from "react";

import { bff } from "@/lib/bff/client";
import { useCartStore } from "@/lib/cart/store";
import type { Cart } from "@/lib/bff/schemas";

interface CartContextValue {
  /** Add a SKU to the cart, creating the cart on first add. */
  addItem: (sku: string, quantity?: number) => Promise<void>;
  /** Update a line item quantity (0 removes it). Optimistic + reconciled. */
  setQuantity: (lineItemId: string, quantity: number) => Promise<void>;
  /** Re-pull the authoritative cart from the BFF. */
  refresh: () => Promise<void>;
}

const CartContext = React.createContext<CartContextValue | null>(null);

export function CartProvider({
  children,
  initialCart,
}: {
  children: React.ReactNode;
  /** Optional server-read cart to seed the mirror without a flash. */
  initialCart?: Cart;
}) {
  const hydrate = useCartStore((s) => s.hydrate);
  const setStatus = useCartStore((s) => s.setStatus);
  const optimisticSetQuantity = useCartStore((s) => s.optimisticSetQuantity);

  // Seed the Zustand mirror once from any server-provided cart.
  React.useEffect(() => {
    if (initialCart) hydrate(initialCart);
    // Seed only on first mount; `initialCart` is a server snapshot.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const refresh = React.useCallback(async () => {
    const cartId = useCartStore.getState().cartId;
    if (!cartId) return;
    const cart = await bff.getCart(cartId);
    hydrate(cart);
  }, [hydrate]);

  const addItem = React.useCallback(
    async (sku: string, quantity = 1) => {
      setStatus("loading");
      try {
        let cartId = useCartStore.getState().cartId;
        if (!cartId) {
          const created = await bff.createCart();
          hydrate(created);
          cartId = created.id;
        }
        const updated = await bff.addLineItem(cartId, { sku, quantity });
        hydrate(updated);
      } catch (err) {
        setStatus("error");
        throw err;
      }
    },
    [hydrate, setStatus]
  );

  const setQuantity = React.useCallback(
    async (lineItemId: string, quantity: number) => {
      const { cartId, lineItems } = useCartStore.getState();
      if (!cartId) return;
      const previous = lineItems;
      optimisticSetQuantity(lineItemId, quantity); // optimistic UI
      try {
        const updated = await bff.updateLineItem(
          cartId,
          lineItemId,
          quantity
        );
        hydrate(updated);
      } catch (err) {
        // Roll back the optimistic update on failure.
        useCartStore.setState({ lineItems: previous });
        setStatus("error");
        throw err;
      }
    },
    [hydrate, optimisticSetQuantity, setStatus]
  );

  const value = React.useMemo<CartContextValue>(
    () => ({ addItem, setQuantity, refresh }),
    [addItem, setQuantity, refresh]
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart(): CartContextValue {
  const ctx = React.useContext(CartContext);
  if (!ctx) {
    throw new Error("useCart must be used within a CartProvider");
  }
  return ctx;
}
