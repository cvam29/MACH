import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ProductCard } from "@/components/product/product-card";
import { ProductGridSkeleton } from "@/components/layout/product-grid-skeleton";
import {
  getContentEntry,
  getFeaturedProducts,
} from "@/lib/server/catalog";
import { pickString } from "@/lib/content";

/**
 * Home — composability shop window.
 *
 * Hero/promo copy is authored in Contentstack (read via the BFF `content`
 * route); the featured grid is commercetools merchandise (read via the BFF
 * `search`). Both reads are resilient: if the BFF is offline the page renders a
 * sensible default hero and an empty-state grid rather than failing.
 */
export default async function HomePage() {
  // Fetch hero content and featured products in parallel; both fall back safely.
  const [hero, featured] = await Promise.all([
    getContentEntry("home-hero", "default"),
    getFeaturedProducts(8),
  ]);

  const eyebrow =
    pickString(hero?.fields, ["eyebrow", "badge", "kicker"]) ??
    "Composable commerce demo";
  const headline =
    pickString(hero?.fields, ["headline", "title"]) ??
    hero?.title ??
    "One product card, three vendors, zero lock-in.";
  const subhead =
    pickString(hero?.fields, ["subhead", "body", "description"]) ??
    "A MACH storefront where commercetools pricing, Algolia discoverability and Contentstack editorial meet behind a single .NET Backend-for-Frontend.";
  const ctaLabel = pickString(hero?.fields, ["ctaLabel"]) ?? "Browse the catalog";
  const ctaHref = pickString(hero?.fields, ["ctaHref", "ctaUrl"]) ?? "/search";

  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
      {/* Hero — Contentstack-authored copy. */}
      <section className="from-muted to-background relative overflow-hidden rounded-2xl border bg-linear-to-br px-6 py-16 sm:px-12 sm:py-24">
        <Badge variant="secondary" className="mb-4">
          {eyebrow}
        </Badge>
        <h1 className="max-w-2xl text-3xl font-semibold tracking-tight text-balance sm:text-5xl">
          {headline}
        </h1>
        <p className="text-muted-foreground mt-4 max-w-xl text-base sm:text-lg">
          {subhead}
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <Button asChild size="lg">
            <Link href={ctaHref}>{ctaLabel}</Link>
          </Button>
          <Button asChild size="lg" variant="outline">
            <Link href="/catalog/new">What&apos;s new</Link>
          </Button>
        </div>
      </section>

      <section className="mt-12">
        <div className="mb-6 flex items-end justify-between">
          <h2 className="text-xl font-semibold tracking-tight">Featured</h2>
          <Button asChild variant="link" size="sm">
            <Link href="/search">View all</Link>
          </Button>
        </div>

        {featured.length > 0 ? (
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            {featured.map((product) => (
              <ProductCard key={product.slug} product={product} />
            ))}
          </div>
        ) : (
          <EmptyFeatured />
        )}
      </section>
    </div>
  );
}

/**
 * Shown when the BFF returned no featured products (offline or empty catalog).
 * Renders the loading frame so the layout stays intact, plus a small notice.
 */
function EmptyFeatured() {
  return (
    <div className="space-y-4">
      <p className="text-muted-foreground text-sm">
        No featured products to show yet. Connect the BFF (commercetools) to see
        live merchandise here.
      </p>
      <div aria-hidden className="opacity-40">
        <ProductGridSkeleton count={4} />
      </div>
    </div>
  );
}
