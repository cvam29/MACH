/**
 * Algolia seed (run AFTER commercetools).
 *
 * Reads the shared catalog, flattens products to flat search records, configures
 * the index (searchable attributes, facets, custom ranking) and provisions a
 * query-suggestions index.
 *
 * Idempotency: records use a stable objectID (the product key) so re-running
 * replaces in place. Settings are declarative — re-applying is a no-op. We also
 * clear stale objects so a re-run is a faithful reflection of catalog.json.
 */
import algoliasearchModule from 'algoliasearch';
import type { SearchClient, SearchIndex } from 'algoliasearch';

// algoliasearch v4 ships a callable default export with a merged namespace; the
// re-export in its index.d.ts can resolve the default import to the namespace
// object under NodeNext, so normalize to the callable factory here.
type AlgoliaFactory = (appId: string, apiKey: string) => SearchClient;
const imported = algoliasearchModule as unknown as AlgoliaFactory & { default?: AlgoliaFactory };
const algoliasearch: AlgoliaFactory = imported.default ?? imported;
import { requireEnv, optionalEnv, runSeed } from '../lib/env.js';
import { loadCatalog, type Catalog, type CatalogProduct } from '../lib/catalog.js';

interface ProductRecord {
  objectID: string;
  sku: string;
  slug: string;
  name: string;
  description: string;
  brand: string;
  category: string;
  categorySlug: string;
  color: string;
  size: string;
  material: string;
  /** List price as a major-unit number so range facets / sorting work naturally. */
  price: number;
  /** Discounted price when on promo, else equal to `price`. */
  priceWithDiscount: number;
  onSale: boolean;
  currency: string;
  inStock: boolean;
  inventoryQuantity: number;
  /** Higher = ranked first in custom ranking. */
  popularity: number;
}

function toRecord(product: CatalogProduct, catalog: Catalog): ProductRecord {
  const category = catalog.categories.find((c) => c.key === product.categoryKey);
  const price = product.priceCents / 100;
  const discounted = (product.discountedPriceCents ?? product.priceCents) / 100;
  return {
    objectID: product.key,
    sku: product.sku,
    slug: product.slug,
    name: product.name,
    description: product.description,
    brand: product.brand,
    category: category?.name ?? product.categoryKey,
    categorySlug: category?.slug ?? product.categoryKey,
    color: product.color,
    size: product.size,
    material: product.material,
    price,
    priceWithDiscount: discounted,
    onSale: product.discountedPriceCents !== undefined,
    currency: catalog.currency,
    inStock: product.inventoryQuantity > 0,
    inventoryQuantity: product.inventoryQuantity,
    // Simple deterministic popularity signal so customRanking has something to sort on.
    popularity: product.inventoryQuantity + (product.discountedPriceCents !== undefined ? 50 : 0),
  };
}

async function configureIndex(index: SearchIndex): Promise<void> {
  await index
    .setSettings({
      searchableAttributes: [
        'name',
        'brand',
        'category',
        'unordered(description)',
        'color',
        'material',
      ],
      attributesForFaceting: [
        'searchable(brand)',
        'searchable(category)',
        'color',
        'size',
        'material',
        'onSale',
        'inStock',
        'filterOnly(price)',
      ],
      customRanking: ['desc(inStock)', 'desc(popularity)', 'asc(priceWithDiscount)'],
      attributesToSnippet: ['description:30'],
      attributesToRetrieve: ['*'],
      replicas: [],
    })
    .wait();
}

async function configureQuerySuggestions(
  client: SearchClient,
  suggestionsIndexName: string,
): Promise<void> {
  // The Query Suggestions index is normally built by Algolia's QS service from
  // the source index + analytics. Without analytics in a fresh sandbox we seed a
  // minimal suggestions index directly from catalog facets so autocomplete has
  // content immediately. (When the QS pipeline is configured in the dashboard it
  // will take over and overwrite this.)
  const catalog = loadCatalog();
  const seen = new Set<string>();
  const suggestions: { objectID: string; query: string; popularity: number }[] = [];

  const push = (query: string, popularity: number): void => {
    const norm = query.trim();
    if (!norm || seen.has(norm.toLowerCase())) return;
    seen.add(norm.toLowerCase());
    suggestions.push({ objectID: `qs-${suggestions.length}`, query: norm, popularity });
  };

  for (const category of catalog.categories) push(category.name, 100);
  for (const product of catalog.products) {
    push(product.brand, 80);
    push(product.name, 60);
    push(`${product.color} ${product.name}`, 40);
  }

  const qsIndex = client.initIndex(suggestionsIndexName);
  await qsIndex
    .setSettings({
      searchableAttributes: ['query'],
      customRanking: ['desc(popularity)'],
      attributesForFaceting: [],
    })
    .wait();
  await qsIndex.replaceAllObjects(suggestions, { safe: true });
  console.log(`  seeded ${suggestions.length} query suggestions into '${suggestionsIndexName}'`);
}

async function main(): Promise<void> {
  const env = requireEnv(['ALGOLIA_APP_ID', 'ALGOLIA_ADMIN_API_KEY'] as const);
  const indexName = optionalEnv('ALGOLIA_INDEX_NAME', 'mach_products');
  const suggestionsIndexName = optionalEnv('ALGOLIA_SUGGESTIONS_INDEX_NAME', `${indexName}_query_suggestions`);

  const client: SearchClient = algoliasearch(env.ALGOLIA_APP_ID, env.ALGOLIA_ADMIN_API_KEY);
  const index = client.initIndex(indexName);

  const catalog = loadCatalog();
  const records = catalog.products.map((p) => toRecord(p, catalog));
  console.log(`Indexing ${records.length} products into Algolia index '${indexName}'…`);

  await configureIndex(index);
  // replaceAllObjects atomically reindexes (clear + add) so the index mirrors catalog.json.
  await index.replaceAllObjects(records, { safe: true });
  console.log(`  reindexed ${records.length} records`);

  await configureQuerySuggestions(client, suggestionsIndexName);
}

await runSeed('algolia', main);
