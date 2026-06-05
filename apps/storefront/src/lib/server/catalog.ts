import "server-only";

import {
  ContentEntrySchema,
  ProductSchema,
  SearchResultSchema,
  type ContentEntry,
  type Product,
  type SearchHit,
} from "@/lib/bff/schemas";
import { serverGet } from "@/lib/server/bff";
import type { ProductCardData } from "@/components/product/product-card";

/** Map a BFF/Algolia search hit to the shared product-card shape. */
export function hitToCard(hit: SearchHit): ProductCardData {
  return {
    slug: hit.slug,
    name: hit.name,
    imageUrl: hit.imageUrl ?? null,
    price: hit.price ?? null,
    tag: hit.categories[0] ?? null,
  };
}

/**
 * Featured products for the home grid. The documented BFF surface exposes
 * `GET /search`; an empty query returns the merchandised/featured set. Resolves
 * to an empty list when the BFF is offline.
 */
export async function getFeaturedProducts(
  limit = 8
): Promise<ProductCardData[]> {
  const result = await serverGet("/search?q=", SearchResultSchema, {
    revalidate: 120,
  });
  if (!result) return [];
  return result.hits.slice(0, limit).map(hitToCard);
}

/**
 * Products for a category listing via `GET /search?category=`. Returns the hits
 * plus the reported total so the page can show a count / empty state.
 */
export async function getCategoryProducts(
  category: string,
  page = 0
): Promise<{ products: ProductCardData[]; nbHits: number; nbPages: number }> {
  const params = new URLSearchParams({ q: "", category, page: String(page) });
  const result = await serverGet(
    `/search?${params.toString()}`,
    SearchResultSchema,
    { revalidate: 120 }
  );
  if (!result) return { products: [], nbHits: 0, nbPages: 0 };
  return {
    products: result.hits.map(hitToCard),
    nbHits: result.nbHits,
    nbPages: result.nbPages,
  };
}

/**
 * Read a single merged product (commercetools + Contentstack) via the BFF
 * (`GET /products/{slug}`). Returns `null` when not found or the BFF is offline.
 */
export async function getProductBySlug(
  slug: string
): Promise<Product | null> {
  return serverGet(
    `/products/${encodeURIComponent(slug)}`,
    ProductSchema,
    { revalidate: 60 }
  );
}

/** Read a single Contentstack entry via the BFF (`GET /content/{type}/{slug}`). */
export async function getContentEntry(
  type: string,
  slug: string
): Promise<ContentEntry | null> {
  return serverGet(
    `/content/${encodeURIComponent(type)}/${encodeURIComponent(slug)}`,
    ContentEntrySchema,
    { revalidate: 300 }
  );
}
