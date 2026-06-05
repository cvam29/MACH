/**
 * Zustand cart store.
 *
 * The cart is server-authoritative (commercetools, via the BFF). The store only
 * **mirrors** the BFF state — `{ cartId, version, lineItems, totals }` — and
 * supports optimistic updates that carry the commercetools `version`. The real
 * write goes through `BffClient`; on success we `hydrate` the returned cart, on
 * failure the caller rolls back. See architecture-plan.md "Cart = commercetools".
 */
import { create } from "zustand";

import type { Cart, CartTotals, LineItem } from "@/lib/bff/schemas";

const EMPTY_TOTALS: CartTotals = {
  itemCount: 0,
  subtotal: { centAmount: 0, currencyCode: "USD", fractionDigits: 2 },
  total: { centAmount: 0, currencyCode: "USD", fractionDigits: 2 },
};

export interface CartState {
  cartId: string | null;
  version: number;
  lineItems: LineItem[];
  totals: CartTotals;
  status: "idle" | "loading" | "error";
  /** Replace local mirror with an authoritative cart from the BFF. */
  hydrate: (cart: Cart) => void;
  /** Optimistically bump a line item's quantity (UI only; server confirms). */
  optimisticSetQuantity: (lineItemId: string, quantity: number) => void;
  setStatus: (status: CartState["status"]) => void;
  reset: () => void;
}

export const useCartStore = create<CartState>((set) => ({
  cartId: null,
  version: 0,
  lineItems: [],
  totals: EMPTY_TOTALS,
  status: "idle",

  hydrate: (cart) =>
    set({
      cartId: cart.id,
      version: cart.version,
      lineItems: cart.lineItems,
      totals: cart.totals,
      status: "idle",
    }),

  optimisticSetQuantity: (lineItemId, quantity) =>
    set((state) => {
      const lineItems = state.lineItems
        .map((li) =>
          li.id === lineItemId
            ? {
                ...li,
                quantity,
                totalPrice: {
                  ...li.totalPrice,
                  centAmount: li.unitPrice.centAmount * quantity,
                },
              }
            : li
        )
        .filter((li) => li.quantity > 0);

      const itemCount = lineItems.reduce((n, li) => n + li.quantity, 0);
      return { lineItems, totals: { ...state.totals, itemCount } };
    }),

  setStatus: (status) => set({ status }),

  reset: () =>
    set({
      cartId: null,
      version: 0,
      lineItems: [],
      totals: EMPTY_TOTALS,
      status: "idle",
    }),
}));

/** Convenience selector: total item count for the mini-cart badge. */
export const selectItemCount = (state: CartState): number =>
  state.totals.itemCount;
