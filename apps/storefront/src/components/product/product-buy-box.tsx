"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { Check, Loader2, ShoppingBag } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { formatMoney } from "@/lib/format";
import { useCart } from "@/components/providers/cart-provider";
import type { Product, ProductVariant } from "@/lib/bff/schemas";

/**
 * Interactive PDP buy box: variant selector + add-to-cart wired to the cart
 * store/BFF. Price and availability follow the selected commercetools variant.
 */
export function ProductBuyBox({ product }: { product: Product }) {
  const { addItem } = useCart();
  const router = useRouter();

  const variants = product.variants;
  const [selectedSku, setSelectedSku] = React.useState<string | null>(
    variants[0]?.sku ?? null
  );
  const [status, setStatus] = React.useState<"idle" | "adding" | "added" | "error">(
    "idle"
  );

  const selected: ProductVariant | undefined =
    variants.find((v) => v.sku === selectedSku) ?? variants[0];

  const canAdd = !!selected && selected.inStock && status !== "adding";

  async function handleAdd() {
    if (!selected) return;
    setStatus("adding");
    try {
      await addItem(selected.sku, 1);
      setStatus("added");
      setTimeout(() => setStatus("idle"), 2000);
    } catch {
      setStatus("error");
    }
  }

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        {selected?.price ? (
          <p className="text-2xl font-semibold tabular-nums">
            {formatMoney(selected.price)}
          </p>
        ) : (
          <p className="text-muted-foreground text-sm">Price unavailable</p>
        )}
        <p
          className={cn(
            "text-sm",
            selected?.inStock ? "text-emerald-600" : "text-muted-foreground"
          )}
        >
          {selected?.inStock ? "In stock" : "Out of stock"}
        </p>
      </div>

      {variants.length > 1 && (
        <fieldset className="space-y-2">
          <legend className="text-sm font-medium">Variant</legend>
          <div className="flex flex-wrap gap-2">
            {variants.map((variant) => (
              <button
                key={variant.sku}
                type="button"
                onClick={() => setSelectedSku(variant.sku)}
                aria-pressed={variant.sku === selectedSku}
                disabled={!variant.inStock}
                className={cn(
                  "rounded-md border px-3 py-2 text-sm transition-colors",
                  "disabled:cursor-not-allowed disabled:opacity-40",
                  variant.sku === selectedSku
                    ? "border-primary bg-primary/5 ring-primary ring-1"
                    : "border-input hover:bg-accent"
                )}
              >
                {variantLabel(variant)}
              </button>
            ))}
          </div>
        </fieldset>
      )}

      <div className="flex flex-wrap gap-3">
        <Button
          size="lg"
          onClick={handleAdd}
          disabled={!canAdd}
          className="flex-1 sm:flex-none"
        >
          {status === "adding" ? (
            <>
              <Loader2 className="size-4 animate-spin" /> Adding…
            </>
          ) : status === "added" ? (
            <>
              <Check className="size-4" /> Added
            </>
          ) : (
            <>
              <ShoppingBag className="size-4" /> Add to cart
            </>
          )}
        </Button>
        <Button
          size="lg"
          variant="outline"
          onClick={() => router.push("/cart")}
        >
          View cart
        </Button>
      </div>

      {status === "error" && (
        <p role="alert" className="text-destructive text-sm">
          Could not add to cart. Please try again.
        </p>
      )}

      {selected && (
        <p className="text-muted-foreground text-xs">
          SKU <span className="font-mono">{selected.sku}</span>
        </p>
      )}
    </div>
  );
}

/** Build a readable variant label from its attributes (e.g. "M · Black"). */
function variantLabel(variant: ProductVariant): React.ReactNode {
  const parts = Object.entries(variant.attributes)
    .map(([, value]) => formatAttr(value))
    .filter((v): v is string => v !== null);

  if (parts.length === 0) {
    return <span className="font-mono text-xs">{variant.sku}</span>;
  }
  return (
    <span className="flex items-center gap-1.5">
      {parts.join(" · ")}
      {!variant.inStock && (
        <Badge variant="secondary" className="text-[10px]">
          sold out
        </Badge>
      )}
    </span>
  );
}

function formatAttr(value: unknown): string | null {
  if (typeof value === "string" || typeof value === "number") {
    return String(value);
  }
  if (value && typeof value === "object" && "label" in value) {
    const label = (value as { label: unknown }).label;
    if (typeof label === "string") return label;
  }
  return null;
}
