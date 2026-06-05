/**
 * Zod schemas mirroring the Bff.Functions API surface (see architecture-plan.md
 * "API surface"). These are the wire shapes the storefront validates responses
 * against. They intentionally model only what the shell needs today; Wave 2
 * widens them as real endpoints land. Keep field names aligned with the .NET
 * `Mach.Contracts` DTOs.
 */
import { z } from "zod";

/* ------------------------------------------------------------------ money --- */

export const MoneySchema = z.object({
  /** Minor units (cents), commercetools `centAmount`. */
  centAmount: z.number().int(),
  currencyCode: z.string().length(3),
  fractionDigits: z.number().int().default(2),
});
export type Money = z.infer<typeof MoneySchema>;

/* -------------------------------------------------------------- catalog --- */

export const CategorySchema = z.object({
  id: z.string(),
  slug: z.string(),
  name: z.string(),
  parentId: z.string().nullable().optional(),
});
export type Category = z.infer<typeof CategorySchema>;

export const CategoryListSchema = z.object({
  categories: z.array(CategorySchema),
});
export type CategoryList = z.infer<typeof CategoryListSchema>;

/* --------------------------------------------------------------- search --- */

export const SearchHitSchema = z.object({
  objectID: z.string(),
  slug: z.string(),
  name: z.string(),
  imageUrl: z.string().url().nullable().optional(),
  price: MoneySchema.nullable().optional(),
  categories: z.array(z.string()).default([]),
});
export type SearchHit = z.infer<typeof SearchHitSchema>;

export const SearchResultSchema = z.object({
  query: z.string(),
  hits: z.array(SearchHitSchema),
  page: z.number().int().nonnegative(),
  nbPages: z.number().int().nonnegative(),
  nbHits: z.number().int().nonnegative(),
});
export type SearchResult = z.infer<typeof SearchResultSchema>;

export const SearchSuggestSchema = z.object({
  suggestions: z.array(z.object({ query: z.string(), count: z.number().int() })),
});
export type SearchSuggest = z.infer<typeof SearchSuggestSchema>;

/* -------------------------------------------------------------- product --- */

export const ProductVariantSchema = z.object({
  sku: z.string(),
  price: MoneySchema.nullable().optional(),
  inStock: z.boolean().default(true),
  attributes: z.record(z.string(), z.unknown()).default({}),
});
export type ProductVariant = z.infer<typeof ProductVariantSchema>;

export const ProductSchema = z.object({
  id: z.string(),
  slug: z.string(),
  name: z.string(),
  description: z.string().nullable().optional(),
  images: z.array(z.string().url()).default([]),
  variants: z.array(ProductVariantSchema).default([]),
  /** Contentstack-authored marketing block, merged server-side by the BFF. */
  marketing: z
    .object({
      headline: z.string().nullable().optional(),
      body: z.string().nullable().optional(),
    })
    .nullable()
    .optional(),
});
export type Product = z.infer<typeof ProductSchema>;

/* ----------------------------------------------------------------- cart --- */

export const LineItemSchema = z.object({
  id: z.string(),
  productId: z.string(),
  slug: z.string(),
  name: z.string(),
  sku: z.string(),
  quantity: z.number().int().positive(),
  unitPrice: MoneySchema,
  totalPrice: MoneySchema,
  imageUrl: z.string().url().nullable().optional(),
});
export type LineItem = z.infer<typeof LineItemSchema>;

export const CartTotalsSchema = z.object({
  itemCount: z.number().int().nonnegative(),
  subtotal: MoneySchema,
  shipping: MoneySchema.nullable().optional(),
  tax: MoneySchema.nullable().optional(),
  total: MoneySchema,
});
export type CartTotals = z.infer<typeof CartTotalsSchema>;

export const CartSchema = z.object({
  /** commercetools cart id — the storefront mirrors only this + version. */
  id: z.string(),
  /** commercetools optimistic-concurrency version. */
  version: z.number().int().nonnegative(),
  lineItems: z.array(LineItemSchema).default([]),
  totals: CartTotalsSchema,
});
export type Cart = z.infer<typeof CartSchema>;

/* ------------------------------------------------------ delivery options --- */

export const DeliveryTypeSchema = z.enum([
  "standard",
  "express",
  "same-day",
  "store-pickup",
]);
export type DeliveryType = z.infer<typeof DeliveryTypeSchema>;

export const DeliveryOptionSchema = z.object({
  type: DeliveryTypeSchema,
  label: z.string(),
  price: MoneySchema,
  /** ISO-8601 estimated delivery window or human label. */
  eta: z.string(),
  available: z.boolean().default(true),
  distanceKm: z.number().nullable().optional(),
});
export type DeliveryOption = z.infer<typeof DeliveryOptionSchema>;

export const DeliveryOptionsSchema = z.object({
  options: z.array(DeliveryOptionSchema),
});
export type DeliveryOptions = z.infer<typeof DeliveryOptionsSchema>;

/* --------------------------------------------------------------- stores --- */

export const StoreSchema = z.object({
  id: z.string(),
  name: z.string(),
  /** Single-line address for display. */
  address: z.string().nullable().optional(),
  distanceKm: z.number().nullable().optional(),
  /** Human pickup-ready ETA (e.g. "Ready in 2 hours"). */
  eta: z.string().nullable().optional(),
});
export type Store = z.infer<typeof StoreSchema>;

export const StoreListSchema = z.object({
  stores: z.array(StoreSchema),
});
export type StoreList = z.infer<typeof StoreListSchema>;

/* ------------------------------------------------------------- checkout --- */

export const PaymentSessionSchema = z.object({
  /** Adyen `/sessions` response, handed to Drop-in. */
  sessionId: z.string(),
  sessionData: z.string(),
  clientKey: z.string().optional(),
  amount: MoneySchema,
});
export type PaymentSession = z.infer<typeof PaymentSessionSchema>;

export const OrderStatusSchema = z.enum([
  "created",
  "authorized",
  "paid",
  "fulfilling",
  "shipped",
  "delivered",
  "cancelled",
  "failed",
]);
export type OrderStatus = z.infer<typeof OrderStatusSchema>;

export const OrderSchema = z.object({
  id: z.string(),
  orderNumber: z.string(),
  status: OrderStatusSchema,
  createdAt: z.string(),
  lineItems: z.array(LineItemSchema),
  totals: CartTotalsSchema,
  delivery: DeliveryOptionSchema.nullable().optional(),
});
export type Order = z.infer<typeof OrderSchema>;

export const OrderListSchema = z.object({
  orders: z.array(OrderSchema),
});
export type OrderList = z.infer<typeof OrderListSchema>;

/* -------------------------------------------------------------- content --- */

export const NavigationItemSchema: z.ZodType<{
  label: string;
  href: string;
  children?: { label: string; href: string }[];
}> = z.object({
  label: z.string(),
  href: z.string(),
  children: z.lazy(() => z.array(NavigationItemSchema)).optional(),
});
export type NavigationItem = z.infer<typeof NavigationItemSchema>;

export const NavigationSchema = z.object({
  items: z.array(NavigationItemSchema),
});
export type Navigation = z.infer<typeof NavigationSchema>;

export const ContentEntrySchema = z.object({
  type: z.string(),
  slug: z.string(),
  title: z.string().nullable().optional(),
  /** Free-form CMS payload — Contentstack entry fields. */
  fields: z.record(z.string(), z.unknown()).default({}),
});
export type ContentEntry = z.infer<typeof ContentEntrySchema>;

/* --------------------------------------------------------------- health --- */

export const HealthSchema = z.object({
  status: z.enum(["healthy", "degraded", "unhealthy"]),
  checks: z.record(z.string(), z.string()).optional(),
});
export type Health = z.infer<typeof HealthSchema>;
