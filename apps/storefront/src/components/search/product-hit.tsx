import type { Hit } from "instantsearch.js";

import { ProductCard, type ProductCardData } from "@/components/product/product-card";
import type { Money } from "@/lib/bff/schemas";

/**
 * Shape of an Algolia product record (seeded by the .NET indexer — flattened
 * commercetools product). Fields are optional/defensive because the index is
 * owned outside the storefront and may evolve.
 */
export interface ProductRecord {
  slug?: string;
  name?: string;
  imageUrl?: string;
  image?: string;
  /** Either a commercetools-style Money object or a plain major-unit number. */
  price?: Money | number;
  currency?: string;
  brand?: string;
  categories?: string[];
}

/** Normalize a search record's price into a `Money`, or null when absent. */
function toMoney(record: ProductRecord): Money | null {
  const { price } = record;
  if (price == null) return null;
  if (typeof price === "number") {
    return {
      centAmount: Math.round(price * 100),
      currencyCode: record.currency ?? "USD",
      fractionDigits: 2,
    };
  }
  return price;
}

/** InstantSearch hit -> shared product card. */
export function ProductHit({ hit }: { hit: Hit<ProductRecord> }) {
  const data: ProductCardData = {
    slug: hit.slug ?? hit.objectID,
    name: hit.name ?? "Untitled product",
    imageUrl: hit.imageUrl ?? hit.image ?? null,
    price: toMoney(hit),
    tag: hit.brand ?? hit.categories?.[0] ?? null,
  };
  return <ProductCard product={data} />;
}
