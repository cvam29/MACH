"use client";

import Link from "next/link";
import { ShoppingBag } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { useCartStore } from "@/lib/cart/store";
import { formatMoney } from "@/lib/format";

export function MiniCart() {
  const lineItems = useCartStore((s) => s.lineItems);
  const totals = useCartStore((s) => s.totals);
  const itemCount = totals.itemCount;

  return (
    <Sheet>
      <SheetTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="relative"
          aria-label={`Cart with ${itemCount} item${itemCount === 1 ? "" : "s"}`}
        >
          <ShoppingBag className="size-5" />
          {itemCount > 0 && (
            <Badge
              className="absolute -top-1 -right-1 size-5 justify-center rounded-full p-0 tabular-nums"
              aria-hidden
            >
              {itemCount}
            </Badge>
          )}
        </Button>
      </SheetTrigger>
      <SheetContent className="w-full sm:max-w-md">
        <SheetHeader>
          <SheetTitle>Your cart</SheetTitle>
          <SheetDescription>
            {itemCount === 0
              ? "Your cart is empty."
              : `${itemCount} item${itemCount === 1 ? "" : "s"} in your cart.`}
          </SheetDescription>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto px-4">
          {lineItems.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              Add products to see them here.
            </p>
          ) : (
            <ul className="divide-border divide-y">
              {lineItems.map((li) => (
                <li
                  key={li.id}
                  className="flex items-center justify-between gap-4 py-3"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{li.name}</p>
                    <p className="text-muted-foreground text-xs">
                      Qty {li.quantity}
                    </p>
                  </div>
                  <span className="text-sm tabular-nums">
                    {formatMoney(li.totalPrice)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>

        <SheetFooter>
          <div className="flex items-center justify-between text-sm font-medium">
            <span>Subtotal</span>
            <span className="tabular-nums">{formatMoney(totals.total)}</span>
          </div>
          <Button asChild className="w-full" disabled={itemCount === 0}>
            <Link href="/cart">View cart</Link>
          </Button>
          <Button
            asChild
            variant="outline"
            className="w-full"
            disabled={itemCount === 0}
          >
            <Link href="/checkout">Checkout</Link>
          </Button>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
