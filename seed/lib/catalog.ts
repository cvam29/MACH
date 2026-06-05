/**
 * Shared catalog model + loader, reused by the commercetools and Algolia seeds
 * so both projects stay in lock-step from one source file (catalog.json).
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

export interface CatalogCategory {
  /** Stable key used for idempotent upsert in commercetools. */
  key: string;
  /** URL slug. */
  slug: string;
  name: string;
  description: string;
}

export interface CatalogProduct {
  /** Stable product key (idempotent upsert key in commercetools). */
  key: string;
  /** Unique SKU on the master variant (idempotent inventory + Algolia id). */
  sku: string;
  slug: string;
  name: string;
  description: string;
  /** References CatalogCategory.key. */
  categoryKey: string;
  brand: string;
  color: string;
  size: string;
  material: string;
  /** List price in minor units (cents) of `currency`. */
  priceCents: number;
  /**
   * Optional discounted price in minor units. When set, the commercetools seed
   * adds a product discount and Algolia exposes it for strike-through display.
   */
  discountedPriceCents?: number;
  /** On-hand quantity for the inventory entry (drives availability). */
  inventoryQuantity: number;
}

export interface Catalog {
  currency: string;
  /** ISO country used for prices/inventory channel scoping. */
  country: string;
  categories: CatalogCategory[];
  products: CatalogProduct[];
}

const here = dirname(fileURLToPath(import.meta.url));
const catalogPath = resolve(here, '..', 'catalog.json');

let cached: Catalog | undefined;

/** Loads and lightly validates catalog.json (cached after first read). */
export function loadCatalog(): Catalog {
  if (cached) return cached;
  const raw = readFileSync(catalogPath, 'utf-8');
  const parsed = JSON.parse(raw) as Catalog;

  if (!parsed.currency || !parsed.country) {
    throw new Error('catalog.json is missing `currency` or `country`.');
  }
  if (!Array.isArray(parsed.categories) || parsed.categories.length === 0) {
    throw new Error('catalog.json has no categories.');
  }
  if (!Array.isArray(parsed.products) || parsed.products.length === 0) {
    throw new Error('catalog.json has no products.');
  }

  const categoryKeys = new Set(parsed.categories.map((c) => c.key));
  const seenSkus = new Set<string>();
  const seenKeys = new Set<string>();
  for (const p of parsed.products) {
    if (!categoryKeys.has(p.categoryKey)) {
      throw new Error(`Product '${p.key}' references unknown category '${p.categoryKey}'.`);
    }
    if (seenSkus.has(p.sku)) throw new Error(`Duplicate SKU '${p.sku}' in catalog.json.`);
    if (seenKeys.has(p.key)) throw new Error(`Duplicate product key '${p.key}' in catalog.json.`);
    seenSkus.add(p.sku);
    seenKeys.add(p.key);
  }

  cached = parsed;
  return parsed;
}
