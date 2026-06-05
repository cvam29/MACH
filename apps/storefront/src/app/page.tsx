import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ProductGridSkeleton } from "@/components/layout/product-grid-skeleton";

export default function HomePage() {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
      {/* Hero — Contentstack-authored copy + commercetools featured in Wave 2. */}
      <section className="from-muted to-background relative overflow-hidden rounded-2xl border bg-linear-to-br px-6 py-16 sm:px-12 sm:py-24">
        <Badge variant="secondary" className="mb-4">
          Composable commerce demo
        </Badge>
        <h1 className="max-w-2xl text-3xl font-semibold tracking-tight text-balance sm:text-5xl">
          One product card, three vendors, zero lock-in.
        </h1>
        <p className="text-muted-foreground mt-4 max-w-xl text-base sm:text-lg">
          A MACH storefront where commercetools pricing, Algolia discoverability
          and Contentstack editorial meet behind a single .NET
          Backend-for-Frontend.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <Button asChild size="lg">
            <Link href="/search">Browse the catalog</Link>
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
        <ProductGridSkeleton />
      </section>
    </div>
  );
}
