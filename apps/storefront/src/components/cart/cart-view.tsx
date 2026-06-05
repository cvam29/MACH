"use client";

import * as React from "react";
import Link from "next/link";
import { Loader2, Minus, Plus, Trash2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { RemoteImage } from "@/components/product/remote-image";
import { useCart } from "@/components/providers/cart-provider";
import { useCartStore } from "@/lib/cart/store";
import { formatMoney } from "@/lib/format";
import type { Cart, LineItem } from "@/lib/bff/schemas";

/**
 * Client cart view. Seeds the Zustand mirror from a server-read cart (so the
 * page is correct on first paint), then renders line items with quantity
 * steppers and remove, plus a totals summary. Quantity changes go through the
 * cart provider (optimistic UI -> BFF -> reconcile).
 */
export function CartView({ initialCart }: { initialCart: Cart | null }) {
  const hydrate = useCartStore((s) => s.hydrate);
  const lineItems = useCartStore((s) => s.lineItems);
  const totals = useCartStore((s) => s.totals);
  const cartId = useCartStore((s) => s.cartId);

  // Seed from the server snapshot once on mount.
  React.useEffect(() => {
    if (initialCart) hydrate(initialCart);
    // Seed only on first mount; initialCart is a server snapshot.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const isEmpty = lineItems.length === 0;

  if (isEmpty && !cartId) {
    return <EmptyCart />;
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1fr_22rem]">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            Items ({totals.itemCount})
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isEmpty ? (
            <p className="text-muted-foreground text-sm">Your cart is empty.</p>
          ) : (
            <ul className="divide-border divide-y">
              {lineItems.map((li) => (
                <CartLine key={li.id} item={li} />
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card className="h-fit">
        <CardHeader>
          <CardTitle className="text-base">Summary</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <dl className="space-y-2 text-sm">
            <Row label="Subtotal" value={formatMoney(totals.subtotal)} />
            {totals.shipping && (
              <Row label="Shipping" value={formatMoney(totals.shipping)} />
            )}
            {totals.tax && <Row label="Tax" value={formatMoney(totals.tax)} />}
            <div className="border-t pt-2">
              <Row
                label="Total"
                value={formatMoney(totals.total)}
                strong
              />
            </div>
          </dl>
          <Button asChild className="w-full" disabled={isEmpty}>
            <Link href="/checkout">Proceed to checkout</Link>
          </Button>
          <Button asChild variant="outline" className="w-full">
            <Link href="/search">Continue shopping</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

function CartLine({ item }: { item: LineItem }) {
  const { setQuantity } = useCart();
  const [pending, setPending] = React.useState(false);

  async function change(quantity: number) {
    setPending(true);
    try {
      await setQuantity(item.id, quantity);
    } catch {
      /* error surfaced via store status; keep UI responsive */
    } finally {
      setPending(false);
    }
  }

  return (
    <li className="flex items-center gap-4 py-4 first:pt-0 last:pb-0">
      <Link
        href={`/product/${encodeURIComponent(item.slug)}`}
        className="shrink-0"
      >
        <RemoteImage
          src={item.imageUrl}
          alt={item.name}
          className="size-16 rounded-md border"
          sizes="64px"
        />
      </Link>

      <div className="min-w-0 flex-1">
        <Link
          href={`/product/${encodeURIComponent(item.slug)}`}
          className="truncate text-sm font-medium hover:underline"
        >
          {item.name}
        </Link>
        <p className="text-muted-foreground text-xs">
          {formatMoney(item.unitPrice)} each
        </p>

        <div className="mt-2 flex items-center gap-1">
          <Button
            type="button"
            variant="outline"
            size="icon"
            className="size-7"
            aria-label="Decrease quantity"
            disabled={pending}
            onClick={() => change(item.quantity - 1)}
          >
            <Minus className="size-3.5" />
          </Button>
          <span className="w-8 text-center text-sm tabular-nums">
            {pending ? (
              <Loader2 className="mx-auto size-3.5 animate-spin" />
            ) : (
              item.quantity
            )}
          </span>
          <Button
            type="button"
            variant="outline"
            size="icon"
            className="size-7"
            aria-label="Increase quantity"
            disabled={pending}
            onClick={() => change(item.quantity + 1)}
          >
            <Plus className="size-3.5" />
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="text-muted-foreground hover:text-destructive ml-1 size-7"
            aria-label="Remove item"
            disabled={pending}
            onClick={() => change(0)}
          >
            <Trash2 className="size-3.5" />
          </Button>
        </div>
      </div>

      <span className="text-sm font-medium tabular-nums">
        {formatMoney(item.totalPrice)}
      </span>
    </li>
  );
}

function Row({
  label,
  value,
  strong,
}: {
  label: string;
  value: string;
  strong?: boolean;
}) {
  return (
    <div className="flex items-center justify-between">
      <dt className={strong ? "font-medium" : "text-muted-foreground"}>
        {label}
      </dt>
      <dd className={strong ? "font-semibold tabular-nums" : "tabular-nums"}>
        {value}
      </dd>
    </div>
  );
}

function EmptyCart() {
  return (
    <Card className="mx-auto max-w-xl">
      <CardContent className="space-y-3 py-12 text-center">
        <p className="text-sm font-medium">Your cart is empty.</p>
        <p className="text-muted-foreground text-sm">
          Add products to get started.
        </p>
        <Button asChild>
          <Link href="/search">Browse products</Link>
        </Button>
      </CardContent>
    </Card>
  );
}
