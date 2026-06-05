/**
 * commercetools seed (run FIRST).
 *
 * Idempotently provisions the apparel sandbox:
 *   - one apparel product type (attributes: size, color, brand, material)
 *   - 3–4 categories
 *   - ~25 products with a single-currency price, master-variant SKU,
 *     inventory entries (drive availability) and a few product discounts
 *   - shipping methods for the delivery types standard / express /
 *     same-day / store-pickup
 *
 * Idempotency: every resource is upserted by its stable `key` (or SKU for
 * inventory). Re-running the script never creates duplicates — it fetches by
 * key and applies update actions, or creates when absent.
 *
 * Reads catalog.json (the shared catalog reused by the Algolia seed).
 */
import { ClientBuilder, type Client } from '@commercetools/sdk-client-v2';
import {
  createApiBuilderFromCtpClient,
  type ByProjectKeyRequestBuilder,
  type ProductType,
  type Category,
  type ShippingMethod,
  type TaxCategory,
} from '@commercetools/platform-sdk';
import { requireEnv, optionalEnv, runSeed } from '../lib/env.js';
import { loadCatalog, type CatalogProduct } from '../lib/catalog.js';

const PRODUCT_TYPE_KEY = 'apparel';
const TAX_CATEGORY_KEY = 'standard-tax';
const DISCOUNT_KEY_PREFIX = 'seed-discount-';

interface ShippingMethodSeed {
  key: string;
  name: string;
  description: string;
  /** Flat price in minor units for this demo (distance pricing is added at runtime by the BFF). */
  priceCents: number;
  isDefault: boolean;
}

const SHIPPING_METHODS: ShippingMethodSeed[] = [
  { key: 'standard', name: 'Standard Delivery', description: '3–5 business days.', priceCents: 499, isDefault: true },
  { key: 'express', name: 'Express Delivery', description: 'Next business day.', priceCents: 1299, isDefault: false },
  { key: 'same-day', name: 'Same-Day Delivery', description: 'Delivered today within the local zone.', priceCents: 1999, isDefault: false },
  { key: 'store-pickup', name: 'Store Pickup', description: 'Collect free from a nearby store.', priceCents: 0, isDefault: false },
];

function buildApiRoot(): { apiRoot: ByProjectKeyRequestBuilder; projectKey: string; client: Client } {
  const env = requireEnv([
    'CTP_PROJECT_KEY',
    'CTP_CLIENT_ID',
    'CTP_CLIENT_SECRET',
    'CTP_AUTH_URL',
    'CTP_API_URL',
  ] as const);
  const scopes = optionalEnv('CTP_SCOPES', `manage_project:${env.CTP_PROJECT_KEY}`)
    .split(/[ ,]+/)
    .filter(Boolean);

  const client = new ClientBuilder()
    .withProjectKey(env.CTP_PROJECT_KEY)
    .withClientCredentialsFlow({
      host: env.CTP_AUTH_URL,
      projectKey: env.CTP_PROJECT_KEY,
      credentials: { clientId: env.CTP_CLIENT_ID, clientSecret: env.CTP_CLIENT_SECRET },
      scopes,
      fetch,
    })
    .withHttpMiddleware({ host: env.CTP_API_URL, fetch })
    .withUserAgentMiddleware()
    .build();

  const apiRoot = createApiBuilderFromCtpClient(client).withProjectKey({ projectKey: env.CTP_PROJECT_KEY });
  return { apiRoot, projectKey: env.CTP_PROJECT_KEY, client };
}

/** True when the SDK threw a 404 (resource not found by key). */
function isNotFound(err: unknown): boolean {
  return typeof err === 'object' && err !== null && 'statusCode' in err && (err as { statusCode: number }).statusCode === 404;
}

async function upsertTaxCategory(apiRoot: ByProjectKeyRequestBuilder, country: string): Promise<TaxCategory> {
  try {
    const existing = await apiRoot.taxCategories().withKey({ key: TAX_CATEGORY_KEY }).get().execute();
    return existing.body;
  } catch (err) {
    if (!isNotFound(err)) throw err;
  }
  const created = await apiRoot
    .taxCategories()
    .post({
      body: {
        key: TAX_CATEGORY_KEY,
        name: 'Standard',
        rates: [{ name: 'Standard', amount: 0.19, includedInPrice: true, country }],
      },
    })
    .execute();
  console.log(`  created tax category '${TAX_CATEGORY_KEY}'`);
  return created.body;
}

async function upsertProductType(apiRoot: ByProjectKeyRequestBuilder): Promise<ProductType> {
  try {
    const existing = await apiRoot.productTypes().withKey({ key: PRODUCT_TYPE_KEY }).get().execute();
    console.log(`  product type '${PRODUCT_TYPE_KEY}' already exists`);
    return existing.body;
  } catch (err) {
    if (!isNotFound(err)) throw err;
  }
  const created = await apiRoot
    .productTypes()
    .post({
      body: {
        key: PRODUCT_TYPE_KEY,
        name: 'Apparel',
        description: 'Apparel product type with size, color, brand and material attributes.',
        attributes: [
          { name: 'size', label: { en: 'Size' }, type: { name: 'text' }, isRequired: false, attributeConstraint: 'None', isSearchable: true, inputHint: 'SingleLine' },
          { name: 'color', label: { en: 'Color' }, type: { name: 'text' }, isRequired: false, attributeConstraint: 'None', isSearchable: true, inputHint: 'SingleLine' },
          { name: 'brand', label: { en: 'Brand' }, type: { name: 'text' }, isRequired: false, attributeConstraint: 'SameForAll', isSearchable: true, inputHint: 'SingleLine' },
          { name: 'material', label: { en: 'Material' }, type: { name: 'text' }, isRequired: false, attributeConstraint: 'None', isSearchable: true, inputHint: 'SingleLine' },
        ],
      },
    })
    .execute();
  console.log(`  created product type '${PRODUCT_TYPE_KEY}'`);
  return created.body;
}

async function upsertCategory(
  apiRoot: ByProjectKeyRequestBuilder,
  category: { key: string; slug: string; name: string; description: string },
): Promise<Category> {
  try {
    const existing = await apiRoot.categories().withKey({ key: category.key }).get().execute();
    return existing.body;
  } catch (err) {
    if (!isNotFound(err)) throw err;
  }
  const created = await apiRoot
    .categories()
    .post({
      body: {
        key: category.key,
        name: { en: category.name },
        slug: { en: category.slug },
        description: { en: category.description },
      },
    })
    .execute();
  console.log(`  created category '${category.key}'`);
  return created.body;
}

async function upsertProduct(
  apiRoot: ByProjectKeyRequestBuilder,
  product: CatalogProduct,
  productTypeId: string,
  categoryIdByKey: Map<string, string>,
  taxCategoryId: string,
  currency: string,
  country: string,
): Promise<void> {
  const categoryId = categoryIdByKey.get(product.categoryKey);
  if (!categoryId) throw new Error(`Category '${product.categoryKey}' not provisioned for product '${product.key}'.`);

  try {
    await apiRoot.products().withKey({ key: product.key }).get().execute();
    // Already present — keep idempotent and cheap; we do not mutate existing products here.
    console.log(`  product '${product.key}' exists — skipped`);
    return;
  } catch (err) {
    if (!isNotFound(err)) throw err;
  }

  await apiRoot
    .products()
    .post({
      body: {
        key: product.key,
        name: { en: product.name },
        slug: { en: product.slug },
        description: { en: product.description },
        productType: { typeId: 'product-type', id: productTypeId },
        taxCategory: { typeId: 'tax-category', id: taxCategoryId },
        categories: [{ typeId: 'category', id: categoryId }],
        publish: true,
        masterVariant: {
          sku: product.sku,
          key: `${product.key}-master`,
          prices: [{ value: { currencyCode: currency, centAmount: product.priceCents }, country }],
          attributes: [
            { name: 'size', value: product.size },
            { name: 'color', value: product.color },
            { name: 'brand', value: product.brand },
            { name: 'material', value: product.material },
          ],
        },
      },
    })
    .execute();
  console.log(`  created product '${product.key}' (${product.sku})`);
}

async function upsertInventory(
  apiRoot: ByProjectKeyRequestBuilder,
  product: CatalogProduct,
): Promise<void> {
  // Inventory is keyed in commercetools by sku (+ optional supplyChannel); upsert by querying on sku.
  const existing = await apiRoot
    .inventory()
    .get({ queryArgs: { where: `sku="${product.sku}"`, limit: 1 } })
    .execute();

  if (existing.body.results.length > 0) {
    const entry = existing.body.results[0];
    if (entry.quantityOnStock === product.inventoryQuantity) return;
    await apiRoot
      .inventory()
      .withId({ ID: entry.id })
      .post({
        body: {
          version: entry.version,
          actions: [
            { action: 'removeQuantity', quantity: entry.quantityOnStock },
            { action: 'addQuantity', quantity: product.inventoryQuantity },
          ],
        },
      })
      .execute();
    return;
  }

  await apiRoot
    .inventory()
    .post({ body: { sku: product.sku, quantityOnStock: product.inventoryQuantity } })
    .execute();
}

async function upsertProductDiscount(
  apiRoot: ByProjectKeyRequestBuilder,
  product: CatalogProduct,
  currency: string,
): Promise<void> {
  if (product.discountedPriceCents === undefined) return;
  const key = `${DISCOUNT_KEY_PREFIX}${product.key}`;

  try {
    await apiRoot.productDiscounts().withKey({ key }).get().execute();
    return; // discount already present
  } catch (err) {
    if (!isNotFound(err)) throw err;
  }

  await apiRoot
    .productDiscounts()
    .post({
      body: {
        key,
        name: { en: `${product.name} promo` },
        value: {
          type: 'absolute',
          money: [{ currencyCode: currency, centAmount: product.priceCents - product.discountedPriceCents }],
        },
        predicate: `sku="${product.sku}"`,
        sortOrder: randomSortOrder(product.key),
        isActive: true,
      },
    })
    .execute();
  console.log(`  created discount for '${product.key}' (-${product.priceCents - product.discountedPriceCents} cents)`);
}

/** Deterministic sortOrder in (0,1) derived from the key (commercetools requires unique values). */
function randomSortOrder(seed: string): string {
  let h = 0;
  for (let i = 0; i < seed.length; i += 1) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
  // Map to a 0.xxxxxxxx fraction, never 0 or 1.
  const frac = (h % 8_999_999) + 1_000_000; // 7-digit, 1000000..9999999
  return `0.${frac.toString().padStart(7, '0')}`;
}

async function upsertShippingMethod(
  apiRoot: ByProjectKeyRequestBuilder,
  method: ShippingMethodSeed,
  taxCategoryId: string,
  currency: string,
  country: string,
): Promise<ShippingMethod | undefined> {
  try {
    const existing = await apiRoot.shippingMethods().withKey({ key: method.key }).get().execute();
    console.log(`  shipping method '${method.key}' exists — skipped`);
    return existing.body;
  } catch (err) {
    if (!isNotFound(err)) throw err;
  }

  // A zone is required for shipping rates; reuse a single seed zone covering the demo country.
  const zoneId = await ensureZone(apiRoot, country);

  const created = await apiRoot
    .shippingMethods()
    .post({
      body: {
        key: method.key,
        name: method.name,
        description: method.description,
        isDefault: method.isDefault,
        taxCategory: { typeId: 'tax-category', id: taxCategoryId },
        zoneRates: [
          {
            zone: { typeId: 'zone', id: zoneId },
            shippingRates: [{ price: { currencyCode: currency, centAmount: method.priceCents } }],
          },
        ],
      },
    })
    .execute();
  console.log(`  created shipping method '${method.key}'`);
  return created.body;
}

async function ensureZone(apiRoot: ByProjectKeyRequestBuilder, country: string): Promise<string> {
  const key = 'seed-zone';
  try {
    const existing = await apiRoot.zones().withKey({ key }).get().execute();
    return existing.body.id;
  } catch (err) {
    if (!isNotFound(err)) throw err;
  }
  const created = await apiRoot
    .zones()
    .post({ body: { key, name: 'Seed Zone', locations: [{ country }] } })
    .execute();
  return created.body.id;
}

async function main(): Promise<void> {
  const { apiRoot } = buildApiRoot();
  const catalog = loadCatalog();
  console.log(`Seeding commercetools with ${catalog.products.length} products in ${catalog.categories.length} categories…`);

  const taxCategory = await upsertTaxCategory(apiRoot, catalog.country);
  const productType = await upsertProductType(apiRoot);

  const categoryIdByKey = new Map<string, string>();
  for (const category of catalog.categories) {
    const created = await upsertCategory(apiRoot, category);
    categoryIdByKey.set(category.key, created.id);
  }

  for (const product of catalog.products) {
    await upsertProduct(apiRoot, product, productType.id, categoryIdByKey, taxCategory.id, catalog.currency, catalog.country);
    await upsertInventory(apiRoot, product);
    await upsertProductDiscount(apiRoot, product, catalog.currency);
  }

  for (const method of SHIPPING_METHODS) {
    await upsertShippingMethod(apiRoot, method, taxCategory.id, catalog.currency, catalog.country);
  }
}

await runSeed('commercetools', main);
